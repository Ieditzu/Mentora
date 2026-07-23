package io.github.kawase.security;

import io.github.kawase.client.ParentSecuritySessionCoordinator;
import io.github.kawase.database.repository.ParentRepository;
import io.github.kawase.database.repository.ParentSessionRepository;
import org.junit.jupiter.api.Test;
import org.springframework.context.annotation.AnnotationConfigApplicationContext;
import org.springframework.core.env.MapPropertySource;

import java.util.Map;

import static org.junit.jupiter.api.Assertions.assertNotNull;
import static org.mockito.Mockito.mock;

class SecurityBeanWiringTest {
    @Test
    void springSelectsTheConfiguredSecurityConstructors() {
        try (final AnnotationConfigApplicationContext context = new AnnotationConfigApplicationContext()) {
            context.getEnvironment().getPropertySources().addFirst(new MapPropertySource(
                    "security-test",
                    Map.of(
                            "mentora.security.totp-encryption-key",
                            "MDEyMzQ1Njc4OWFiY2RlZjAxMjM0NTY3ODlhYmNkZWY=",
                            "mentora.security.parent-session-ttl-days",
                            "30",
                            "mentora.security.login-challenge-ttl-seconds",
                            "300",
                            "mentora.security.login-challenge-attempt-limit",
                            "5",
                            "mentora.security.enrollment-ttl-seconds",
                            "600"
                    )
            ));
            context.registerBean(ParentRepository.class, () -> mock(ParentRepository.class));
            context.registerBean(ParentSessionRepository.class, () -> mock(ParentSessionRepository.class));
            context.register(
                    ParentSessionService.class,
                    TotpSecretCipher.class,
                    TotpCodeService.class,
                    ParentPasswordService.class,
                    ParentAuthenticationService.class,
                    ParentSecuritySessionCoordinator.class
            );
            context.refresh();

            assertNotNull(context.getBean(ParentSessionService.class));
            assertNotNull(context.getBean(TotpSecretCipher.class));
            assertNotNull(context.getBean(ParentAuthenticationService.class));
            assertNotNull(context.getBean(ParentSecuritySessionCoordinator.class));
        }
    }
}
