package io.github.kawase.exceptions;

public class PacketException extends RuntimeException {
    public PacketException(final String message) {
        super(message);
    }

    public PacketException(final String message, final Throwable cause) {
        super(message, cause);
    }
}
