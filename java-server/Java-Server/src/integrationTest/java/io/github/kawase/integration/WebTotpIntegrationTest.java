package io.github.kawase.integration;

import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;
import io.github.kawase.database.entity.Parent;
import io.github.kawase.database.entity.ParentSession;
import io.github.kawase.database.repository.ParentRepository;
import io.github.kawase.database.repository.ParentSessionRepository;
import io.github.kawase.security.ParentSessionService;
import io.github.kawase.security.TotpCodeService;
import io.github.kawase.utility.HashUtility;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.test.context.SpringBootTest;
import org.springframework.boot.test.web.client.TestRestTemplate;
import org.springframework.http.HttpEntity;
import org.springframework.http.HttpHeaders;
import org.springframework.http.HttpMethod;
import org.springframework.http.HttpStatus;
import org.springframework.http.MediaType;
import org.springframework.http.ResponseEntity;
import org.springframework.test.annotation.DirtiesContext;
import org.testcontainers.junit.jupiter.Testcontainers;

import java.util.Map;
import java.util.UUID;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertFalse;
import static org.junit.jupiter.api.Assertions.assertNotEquals;
import static org.junit.jupiter.api.Assertions.assertTrue;

@Testcontainers(disabledWithoutDocker = true)
@SpringBootTest(webEnvironment = SpringBootTest.WebEnvironment.RANDOM_PORT)
@DirtiesContext(classMode = DirtiesContext.ClassMode.AFTER_CLASS)
class WebTotpIntegrationTest extends PostgresIntegrationTestSupport {
    @Autowired
    private TestRestTemplate rest;

    @Autowired
    private ObjectMapper objectMapper;

    @Autowired
    private TotpCodeService totpCodeService;

    @Autowired
    private ParentRepository parentRepository;

    @Autowired
    private ParentSessionRepository parentSessionRepository;

    @Autowired
    private ParentSessionService parentSessionService;

    @Test
    void parentEnrollsChallengesLogsInWithRecoveryCodeAndDisablesTotp() throws Exception {
        final String email = "totp-" + UUID.randomUUID() + "@mentora.test";
        final String password = "Correct Horse Battery Staple";
        final JsonNode registration = body(rest.postForEntity(
                "/api/web/auth/register",
                Map.of("email", email, "password", password),
                String.class
        ));
        final String enrollmentToken = registration.path("token").asText();

        final JsonNode setup = authenticatedExchange(
                HttpMethod.POST,
                "/api/web/auth/totp/setup",
                enrollmentToken,
                Map.of("password", password),
                HttpStatus.OK
        );
        assertTrue(setup.path("otpAuthUri").asText().startsWith("otpauth://totp/"));
        final JsonNode enabled = authenticatedExchange(
                HttpMethod.POST,
                "/api/web/auth/totp/enable",
                enrollmentToken,
                Map.of(
                        "enrollmentId", setup.path("enrollmentId").asText(),
                        "code", totpCodeService.generateCurrentCode(setup.path("secretBase32").asText())
                ),
                HttpStatus.OK
        );
        assertTrue(enabled.path("success").asBoolean());
        assertEquals(10, enabled.path("recoveryCodes").size());
        final Parent persistedParent = parentRepository.findByEmail(HashUtility.hash(email)).orElseThrow();
        assertTrue(persistedParent.getTotpEnabled());
        assertNotEquals(setup.path("secretBase32").asText(), persistedParent.getTotpSecretEncrypted());
        assertFalse(persistedParent.getTotpSecretEncrypted().contains(setup.path("secretBase32").asText()));
        assertFalse(persistedParent.getTotpRecoveryCodeHashes()
                .contains(enabled.path("recoveryCodes").get(0).asText()));
        assertTrue(parentSessionRepository.findByParentIdAndRevokedAtIsNull(persistedParent.getId()).isEmpty());

        authenticatedExchange(
                HttpMethod.GET,
                "/api/web/auth/security",
                enrollmentToken,
                null,
                HttpStatus.UNAUTHORIZED
        );

        final ResponseEntity<String> passwordLogin = rest.postForEntity(
                "/api/web/auth/login",
                Map.of("email", email.toUpperCase(), "password", password),
                String.class
        );
        assertEquals(HttpStatus.ACCEPTED, passwordLogin.getStatusCode());
        final JsonNode challenge = body(passwordLogin);
        assertTrue(challenge.path("requiresTotp").asBoolean());
        assertFalse(challenge.path("challengeId").asText().isBlank());

        final JsonNode verified = body(rest.postForEntity(
                "/api/web/auth/login/totp",
                Map.of(
                        "challengeId", challenge.path("challengeId").asText(),
                        "code", enabled.path("recoveryCodes").get(0).asText()
                ),
                String.class
        ));
        final String originalVerifiedToken = verified.path("token").asText();
        assertFalse(originalVerifiedToken.isBlank());
        final ParentSession persistedSession = parentSessionRepository
                .findByParentIdAndRevokedAtIsNull(persistedParent.getId())
                .getFirst();
        assertNotEquals(originalVerifiedToken, persistedSession.getTokenHash());
        assertEquals(HashUtility.hash(originalVerifiedToken), persistedSession.getTokenHash());

        final ParentSessionService.SessionToken resumedSession = parentSessionService
                .resume(originalVerifiedToken, "web")
                .orElseThrow();
        final String verifiedToken = resumedSession.rawToken();
        assertFalse(parentSessionService.validate(originalVerifiedToken).isPresent());
        assertTrue(parentSessionService.validate(verifiedToken).isPresent());
        assertFalse(verifiedToken.isBlank());

        final JsonNode securityStatus = authenticatedExchange(
                HttpMethod.GET,
                "/api/web/auth/security",
                verifiedToken,
                null,
                HttpStatus.OK
        );
        assertTrue(securityStatus.path("totpEnabled").asBoolean());
        assertEquals(9, securityStatus.path("recoveryCodesRemaining").asInt());

        final JsonNode disabled = authenticatedExchange(
                HttpMethod.DELETE,
                "/api/web/auth/totp",
                verifiedToken,
                Map.of(
                        "password", password,
                        "code", enabled.path("recoveryCodes").get(1).asText()
                ),
                HttpStatus.OK
        );
        assertTrue(disabled.path("success").asBoolean());

        final ResponseEntity<String> passwordOnlyLogin = rest.postForEntity(
                "/api/web/auth/login",
                Map.of("email", email, "password", password),
                String.class
        );
        assertEquals(HttpStatus.OK, passwordOnlyLogin.getStatusCode());
        assertFalse(body(passwordOnlyLogin).path("requiresTotp").asBoolean());
    }

    private JsonNode authenticatedExchange(
            final HttpMethod method,
            final String path,
            final String token,
            final Object requestBody,
            final HttpStatus expectedStatus) throws Exception {
        final HttpHeaders headers = new HttpHeaders();
        headers.setBearerAuth(token);
        headers.setContentType(MediaType.APPLICATION_JSON);
        final ResponseEntity<String> response = rest.exchange(
                path,
                method,
                new HttpEntity<>(requestBody, headers),
                String.class
        );
        assertEquals(expectedStatus, response.getStatusCode(), response.getBody());
        return body(response);
    }

    private JsonNode body(final ResponseEntity<String> response) throws Exception {
        return objectMapper.readTree(response.getBody());
    }
}
