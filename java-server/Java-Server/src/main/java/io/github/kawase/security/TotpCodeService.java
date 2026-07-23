package io.github.kawase.security;

import org.springframework.stereotype.Component;

import javax.crypto.Mac;
import javax.crypto.spec.SecretKeySpec;
import java.nio.ByteBuffer;
import java.nio.charset.StandardCharsets;
import java.security.MessageDigest;
import java.security.SecureRandom;
import java.time.Clock;
import java.time.Instant;
import java.util.OptionalLong;

@Component
public class TotpCodeService {
    private static final int PERIOD_SECONDS = 30;
    private static final int DIGITS = 6;

    private final Clock clock;
    private final SecureRandom secureRandom;

    public TotpCodeService() {
        this(Clock.systemUTC(), new SecureRandom());
    }

    TotpCodeService(final Clock clock, final SecureRandom secureRandom) {
        this.clock = clock;
        this.secureRandom = secureRandom;
    }

    public String generateSecret() {
        final byte[] secret = new byte[20];
        secureRandom.nextBytes(secret);
        return Base32Codec.encode(secret);
    }

    public String generateCurrentCode(final String secret) {
        return generateCode(secret, currentStep(), DIGITS);
    }

    public OptionalLong findMatchingStep(final String secret, final String suppliedCode) {
        if (suppliedCode == null || !suppliedCode.matches("\\d{" + DIGITS + "}"))
            return OptionalLong.empty();

        final long currentStep = currentStep();
        for (long step = currentStep - 1; step <= currentStep + 1; step++) {
            if (MessageDigest.isEqual(
                    generateCode(secret, step, DIGITS).getBytes(StandardCharsets.US_ASCII),
                    suppliedCode.getBytes(StandardCharsets.US_ASCII)
            ))
                return OptionalLong.of(step);
        }
        return OptionalLong.empty();
    }

    String generateCode(final String secret, final long step, final int digits) {
        try {
            final Mac mac = Mac.getInstance("HmacSHA1");
            mac.init(new SecretKeySpec(Base32Codec.decode(secret), "HmacSHA1"));
            final byte[] digest = mac.doFinal(ByteBuffer.allocate(Long.BYTES).putLong(step).array());
            final int offset = digest[digest.length - 1] & 0x0f;
            final int binary = ((digest[offset] & 0x7f) << 24)
                    | ((digest[offset + 1] & 0xff) << 16)
                    | ((digest[offset + 2] & 0xff) << 8)
                    | (digest[offset + 3] & 0xff);
            final int modulus = (int) Math.pow(10, digits);
            return String.format("%0" + digits + "d", binary % modulus);
        } catch (Exception exception) {
            throw new IllegalStateException("Could not generate TOTP code", exception);
        }
    }

    long stepAt(final Instant instant) {
        return instant.getEpochSecond() / PERIOD_SECONDS;
    }

    private long currentStep() {
        return stepAt(clock.instant());
    }
}
