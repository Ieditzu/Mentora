package io.github.kawase.web;

import io.github.kawase.database.entity.Parent;
import io.github.kawase.database.services.ParentService;
import io.github.kawase.security.ParentAuthenticationService;
import io.github.kawase.utility.HashUtility;
import jakarta.servlet.http.HttpServletRequest;
import lombok.RequiredArgsConstructor;
import org.springframework.http.HttpStatus;
import org.springframework.http.ResponseEntity;
import org.springframework.web.bind.annotation.DeleteMapping;
import org.springframework.web.bind.annotation.ExceptionHandler;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RestController;

import java.util.Map;

@RestController
@RequestMapping("/api/web/auth")
@RequiredArgsConstructor
public class WebAuthController {
    private final ParentService parentService;
    private final WebSessionService webSessionService;
    private final ParentAuthenticationService parentAuthenticationService;

    @PostMapping("/lookup")
    public LookupResponse lookup(@RequestBody final EmailLookupRequest request) {
        validateEmail(request == null ? null : request.email());
        final String emailHash = HashUtility.hash(request.email().trim().toLowerCase());
        final boolean exists = parentService.findByEmail(emailHash).isPresent();
        return new LookupResponse(exists);
    }

    @PostMapping("/register")
    public AuthResponse register(@RequestBody final AuthRequest request) {
        validateAuthRequest(request);
        final Parent parent = parentService.createParentAccount(
                HashUtility.hash(request.email().trim().toLowerCase()),
                HashUtility.hash(request.password())
        );
        final var session = parentAuthenticationService.issueSession(parent.getId(), "web");
        return AuthResponse.authenticated(session.parentId(), session.rawToken());
    }

    @PostMapping("/login")
    public ResponseEntity<AuthResponse> login(@RequestBody final AuthRequest request) {
        validateAuthRequest(request);
        final ParentAuthenticationService.LoginResult result = parentAuthenticationService.authenticatePassword(
                HashUtility.hash(request.email().trim().toLowerCase()),
                HashUtility.hash(request.password()),
                "web",
                "web"
        );
        if (result.secondFactorRequired())
            return ResponseEntity.status(HttpStatus.ACCEPTED).body(AuthResponse.secondFactorRequired(
                    result.challengeId(),
                    result.expiresInSeconds()
            ));
        if (!result.success())
            throw new RuntimeException(result.message());
        return ResponseEntity.ok(AuthResponse.authenticated(
                result.session().parentId(),
                result.session().rawToken()
        ));
    }

    @PostMapping("/login/totp")
    public AuthResponse verifyTotpLogin(@RequestBody final TotpLoginRequest request) {
        if (request == null || request.challengeId() == null || request.code() == null)
            throw new RuntimeException("Challenge and code are required");

        final ParentAuthenticationService.LoginResult result = parentAuthenticationService.verifySecondFactor(
                request.challengeId(),
                request.code(),
                "web",
                "web"
        );
        if (!result.success())
            throw new RuntimeException(result.message());
        return AuthResponse.authenticated(result.session().parentId(), result.session().rawToken());
    }

    @GetMapping("/security")
    public ParentAuthenticationService.SecurityStatus securityStatus(final HttpServletRequest request) {
        return parentAuthenticationService.getSecurityStatus(requireParentId(request));
    }

    @PostMapping("/totp/setup")
    public ParentAuthenticationService.EnrollmentDetails beginTotpEnrollment(
            final HttpServletRequest servletRequest,
            @RequestBody final TotpSetupRequest request) {
        if (request == null || request.password() == null || request.password().isBlank())
            throw new RuntimeException("Password is required");
        return parentAuthenticationService.beginEnrollment(
                requireParentId(servletRequest),
                HashUtility.hash(request.password())
        );
    }

    @PostMapping("/totp/enable")
    public ParentAuthenticationService.EnrollmentResult confirmTotpEnrollment(
            final HttpServletRequest servletRequest,
            @RequestBody final TotpEnrollmentRequest request) {
        if (request == null || request.enrollmentId() == null || request.code() == null)
            throw new RuntimeException("Enrollment and code are required");
        return parentAuthenticationService.confirmEnrollment(
                requireParentId(servletRequest),
                request.enrollmentId(),
                request.code()
        );
    }

    @DeleteMapping("/totp")
    public Map<String, Boolean> disableTotp(
            final HttpServletRequest servletRequest,
            @RequestBody final DisableTotpRequest request) {
        if (request == null || request.password() == null || request.code() == null)
            throw new RuntimeException("Password and code are required");
        parentAuthenticationService.disableTotp(
                requireParentId(servletRequest),
                HashUtility.hash(request.password()),
                request.code()
        );
        return Map.of("success", true);
    }

    @ExceptionHandler(RuntimeException.class)
    public ResponseEntity<Map<String, String>> handleError(final RuntimeException exception) {
        final String message = exception.getMessage() == null ? "Unexpected error" : exception.getMessage();
        final HttpStatus status = message.equals("Unauthorized") || message.equals("Invalid credentials")
                ? HttpStatus.UNAUTHORIZED
                : HttpStatus.BAD_REQUEST;
        return ResponseEntity.status(status).body(Map.of("error", message));
    }

    private void validateAuthRequest(final AuthRequest request) {
        if (request == null || request.email() == null || request.email().isBlank()
                || request.password() == null || request.password().isBlank())
            throw new RuntimeException("Email and password are required");
    }

    private void validateEmail(final String email) {
        if (email == null || email.isBlank())
            throw new RuntimeException("Email is required");
    }

    private Long requireParentId(final HttpServletRequest request) {
        final String authorization = request.getHeader("Authorization");
        if (authorization == null || !authorization.startsWith("Bearer "))
            throw new RuntimeException("Unauthorized");
        return webSessionService.requireParentId(authorization.substring("Bearer ".length()).trim());
    }

    public record EmailLookupRequest(String email) {
        /* w */
    }

    public record LookupResponse(boolean exists) {
        /* w */
    }

    public record AuthRequest(String email, String password) {
        /* w */
    }

    public record TotpLoginRequest(String challengeId, String code) {
        /* w */
    }

    public record TotpSetupRequest(String password) {
        /* w */
    }

    public record TotpEnrollmentRequest(String enrollmentId, String code) {
        /* w */
    }

    public record DisableTotpRequest(String password, String code) {
        /* w */
    }

    public record AuthResponse(
            Long parentId,
            String token,
            boolean requiresTotp,
            String challengeId,
            int expiresInSeconds) {
        private static AuthResponse authenticated(final Long parentId, final String token) {
            return new AuthResponse(parentId, token, false, "", 0);
        }

        private static AuthResponse secondFactorRequired(final String challengeId, final int expiresInSeconds) {
            return new AuthResponse(null, "", true, challengeId, expiresInSeconds);
        }
    }
}
