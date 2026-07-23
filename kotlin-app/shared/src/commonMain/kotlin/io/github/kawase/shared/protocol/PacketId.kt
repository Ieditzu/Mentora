package io.github.kawase.shared.protocol

object PacketId {
    const val HANDSHAKE = 1
    const val AUTHENTICATE = 2
    const val REGISTER_PARENT = 3
    const val FETCH_CHILDREN = 15
    const val FETCH_WEEKLY_REPORT = 69
    const val WEEKLY_REPORT_RESPONSE = 70
    const val SET_CLIENT_LANGUAGE = 76
    const val PARENT_SECOND_FACTOR_REQUIRED = 81
    const val VERIFY_PARENT_SECOND_FACTOR = 82
    const val BEGIN_PARENT_TOTP_ENROLLMENT = 83
    const val PARENT_TOTP_ENROLLMENT_DETAILS = 84
    const val CONFIRM_PARENT_TOTP_ENROLLMENT = 85
    const val PARENT_TOTP_ENROLLMENT_RESULT = 86
    const val DISABLE_PARENT_TOTP = 87
    const val FETCH_PARENT_SECURITY_STATUS = 88
    const val PARENT_SECURITY_STATUS = 89
    const val PARENT_AUTH_SESSION = 90
    const val RESUME_PARENT_SESSION = 91
    const val REVOKE_PARENT_SESSION = 92
}
