package io.github.kawase.integration;

import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;
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

import java.util.List;
import java.util.Map;
import java.util.UUID;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertFalse;
import static org.junit.jupiter.api.Assertions.assertNotEquals;
import static org.junit.jupiter.api.Assertions.assertTrue;

@Testcontainers(disabledWithoutDocker = true)
@SpringBootTest(webEnvironment = SpringBootTest.WebEnvironment.RANDOM_PORT)
@DirtiesContext(classMode = DirtiesContext.ClassMode.AFTER_CLASS)
class WebCreatorCourseIntegrationTest extends PostgresIntegrationTestSupport {
    @Autowired
    private TestRestTemplate rest;

    @Autowired
    private ObjectMapper objectMapper;

    @Test
    void creatorRegistersPublishesUpdatesAndDeletesOwnedCourseOverHttp() throws Exception {
        final String email = "creator-" + UUID.randomUUID() + "@mentora.test";
        final JsonNode registration = postJson("/api/web/auth/register", Map.of(
                "email", email,
                "password", "Correct Horse Battery Staple"
        ));
        final long parentId = registration.path("parentId").asLong();
        final String token = registration.path("token").asText();

        assertTrue(parentId > 0);
        assertFalse(token.isBlank());
        assertTrue(postJson("/api/web/auth/lookup", Map.of("email", email.toUpperCase()))
                .path("exists").asBoolean());

        final JsonNode created = exchangeJson(
                HttpMethod.POST,
                "/api/web/courses",
                token,
                coursePayload("Secure Python", "py-sec", true, 35)
        );
        final long courseId = created.path("id").asLong();

        assertTrue(courseId > 0);
        assertEquals("PY-SEC", created.path("acronym").asText());
        assertTrue(created.path("published").asBoolean());
        assertEquals(2, created.path("questions").size());

        final ResponseEntity<String> missingAuthentication = rest.getForEntity(
                "/api/web/courses/mine",
                String.class
        );
        assertEquals(HttpStatus.UNAUTHORIZED, missingAuthentication.getStatusCode());

        final JsonNode secondRegistration = postJson("/api/web/auth/register", Map.of(
                "email", "other-" + UUID.randomUUID() + "@mentora.test",
                "password", "Different Correct Password"
        ));
        final HttpHeaders otherParentHeaders = new HttpHeaders();
        otherParentHeaders.setBearerAuth(secondRegistration.path("token").asText());
        final ResponseEntity<String> crossParentRead = rest.exchange(
                "/api/web/courses/" + courseId,
                HttpMethod.GET,
                new HttpEntity<>(null, otherParentHeaders),
                String.class
        );
        assertEquals(HttpStatus.FORBIDDEN, crossParentRead.getStatusCode());

        final JsonNode mine = exchangeJson(HttpMethod.GET, "/api/web/courses/mine", token, null);
        assertEquals(1, mine.size());
        assertEquals(courseId, mine.get(0).path("id").asLong());

        final JsonNode updated = exchangeJson(
                HttpMethod.PUT,
                "/api/web/courses/" + courseId,
                token,
                coursePayload("Secure Python II", "py-sec-2", true, 55)
        );
        assertEquals("Secure Python II", updated.path("title").asText());
        assertEquals(55, updated.path("pointReward").asInt());

        final JsonNode login = postJson("/api/web/auth/login", Map.of(
                "email", email.toUpperCase(),
                "password", "Correct Horse Battery Staple"
        ));
        assertEquals(parentId, login.path("parentId").asLong());
        assertNotEquals(token, login.path("token").asText());

        final JsonNode deleted = exchangeJson(HttpMethod.DELETE, "/api/web/courses/" + courseId, token, null);
        assertTrue(deleted.path("success").asBoolean());
        assertEquals(0, exchangeJson(HttpMethod.GET, "/api/web/courses/mine", token, null).size());
    }

    private JsonNode postJson(final String path, final Object body) throws Exception {
        final ResponseEntity<String> response = rest.postForEntity(path, body, String.class);
        assertTrue(response.getStatusCode().is2xxSuccessful(), response.getBody());
        return objectMapper.readTree(response.getBody());
    }

    private JsonNode exchangeJson(
            final HttpMethod method,
            final String path,
            final String token,
            final Object body
    ) throws Exception {
        final HttpHeaders headers = new HttpHeaders();
        headers.setBearerAuth(token);
        headers.setContentType(MediaType.APPLICATION_JSON);
        final ResponseEntity<String> response = rest.exchange(
                path,
                method,
                new HttpEntity<>(body, headers),
                String.class
        );
        assertTrue(response.getStatusCode().is2xxSuccessful(), response.getBody());
        return objectMapper.readTree(response.getBody());
    }

    private Map<String, Object> coursePayload(
            final String title,
            final String acronym,
            final boolean published,
            final int reward
    ) {
        return Map.of(
                "title", title,
                "acronym", acronym,
                "language", "python",
                "difficulty", "intermediate",
                "summary", "A creator-authored Python course.",
                "description", "Published through the real creator HTTP boundary.",
                "pointReward", reward,
                "published", published,
                "questions", List.of(
                        question(0, "Which keyword defines a function?", "def", "func", "let", "class", 0),
                        question(1, "What does range(3) produce?", "1,2,3", "0,1,2", "0,1,2,3", "3", 1)
                )
        );
    }

    private Map<String, Object> question(
            final int order,
            final String prompt,
            final String optionA,
            final String optionB,
            final String optionC,
            final String optionD,
            final int correctIndex
    ) {
        return Map.of(
                "orderIndex", order,
                "prompt", prompt,
                "optionA", optionA,
                "optionB", optionB,
                "optionC", optionC,
                "optionD", optionD,
                "correctIndex", correctIndex,
                "explanation", "Server-side course fixture"
        );
    }
}
