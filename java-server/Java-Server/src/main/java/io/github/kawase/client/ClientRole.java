package io.github.kawase.client;

public enum ClientRole {
    UNAUTHENTICATED,
    PASSWORD_VERIFIED_PENDING_TOTP,
    PARENT,
    CHILD
}
