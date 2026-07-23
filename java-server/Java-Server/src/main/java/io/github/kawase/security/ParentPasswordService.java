package io.github.kawase.security;

import io.github.kawase.database.entity.Parent;
import org.springframework.security.crypto.bcrypt.BCryptPasswordEncoder;
import org.springframework.stereotype.Component;

import java.nio.charset.StandardCharsets;
import java.security.MessageDigest;

@Component
public class ParentPasswordService {
    private static final String BCRYPT_PREFIX = "{bcrypt}";
    private final BCryptPasswordEncoder passwordEncoder = new BCryptPasswordEncoder();

    public String encode(final String clientCredentialHash) {
        if (clientCredentialHash == null || clientCredentialHash.isBlank())
            throw new RuntimeException("Password credential is required");
        return BCRYPT_PREFIX + passwordEncoder.encode(clientCredentialHash);
    }

    public boolean matchesAndUpgrade(final Parent parent, final String clientCredentialHash) {
        if (parent == null || parent.getPasswordHash() == null || clientCredentialHash == null)
            return false;

        final String storedCredential = parent.getPasswordHash();
        if (storedCredential.startsWith(BCRYPT_PREFIX))
            return passwordEncoder.matches(
                    clientCredentialHash,
                    storedCredential.substring(BCRYPT_PREFIX.length())
            );

        final boolean matches = MessageDigest.isEqual(
                storedCredential.getBytes(StandardCharsets.UTF_8),
                clientCredentialHash.getBytes(StandardCharsets.UTF_8)
        );
        if (matches)
            parent.setPasswordHash(encode(clientCredentialHash));
        return matches;
    }
}
