package io.github.kawase.socket.exceptions;

public class PacketException extends RuntimeException {
    public PacketException(final String message) {
        super(message);
    }

    public PacketException(final String message, final Throwable cause) {
        super(message, cause);
    }
}
