package io.github.kawase.web;

import io.github.kawase.database.entity.Parent;
import io.github.kawase.database.services.ParentService;
import io.github.kawase.utility.HashUtility;
import lombok.RequiredArgsConstructor;
import org.springframework.web.bind.annotation.ExceptionHandler;
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

    @PostMapping("/register")
    public AuthResponse register(@RequestBody AuthRequest request) {
        validateAuthRequest(request);
        Parent parent = parentService.createParentAccount(
                HashUtility.hash(request.email().trim().toLowerCase()),
                HashUtility.hash(request.password())
        );
        String token = webSessionService.createSession(parent.getId());
        return new AuthResponse(parent.getId(), token);
    }

    @PostMapping("/login")
    public AuthResponse login(@RequestBody AuthRequest request) {
        validateAuthRequest(request);
        String emailHash = HashUtility.hash(request.email().trim().toLowerCase());
        String passwordHash = HashUtility.hash(request.password());
        boolean success = parentService.loginParent(emailHash, passwordHash);
        if (!success) {
            throw new RuntimeException("Invalid credentials");
        }
        Parent parent = parentService.findByEmail(emailHash)
                .orElseThrow(() -> new RuntimeException("Invalid credentials"));
        String token = webSessionService.createSession(parent.getId());
        return new AuthResponse(parent.getId(), token);
    }

    @ExceptionHandler(RuntimeException.class)
    public Map<String, String> handleError(RuntimeException exception) {
        return Map.of("error", exception.getMessage() == null ? "Unexpected error" : exception.getMessage());
    }

    private void validateAuthRequest(final AuthRequest request) {
        if (request == null || request.email() == null || request.email().isBlank() || request.password() == null || request.password().isBlank()) {
            throw new RuntimeException("Email and password are required");
        }
    }

    public record AuthRequest(String email, String password) {}
    public record AuthResponse(Long parentId, String token) {}
}
