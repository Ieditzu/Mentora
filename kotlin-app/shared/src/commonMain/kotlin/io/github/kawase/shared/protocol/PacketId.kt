package io.github.kawase.shared.protocol

object PacketId {
    const val HANDSHAKE = 1
    const val AUTHENTICATE = 2
    const val REGISTER_PARENT = 3
    const val FETCH_CHILDREN = 15
    const val FETCH_WEEKLY_REPORT = 69
    const val WEEKLY_REPORT_RESPONSE = 70
    const val SET_CLIENT_LANGUAGE = 76
}
