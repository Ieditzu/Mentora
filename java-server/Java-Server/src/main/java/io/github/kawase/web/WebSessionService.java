package io.github.kawase.web;

import io.github.kawase.security.ParentSessionService;
import lombok.RequiredArgsConstructor;
import org.springframework.stereotype.Service;

@Service
@RequiredArgsConstructor
public class WebSessionService {
    private final ParentSessionService parentSessionService;

    public String createSession(final Long parentId) {
        return parentSessionService.issue(parentId, "web").rawToken();
    }

    public Long requireParentId(final String token) {
        return parentSessionService.validate(token).orElseThrow(() -> new RuntimeException("Unauthorized"));
    }
}
