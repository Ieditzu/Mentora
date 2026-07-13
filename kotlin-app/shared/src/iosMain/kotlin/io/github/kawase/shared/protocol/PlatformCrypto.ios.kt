package io.github.kawase.shared.protocol

import kotlinx.cinterop.ExperimentalForeignApi
import kotlinx.cinterop.addressOf
import kotlinx.cinterop.alloc
import kotlinx.cinterop.convert
import kotlinx.cinterop.memScoped
import kotlinx.cinterop.ptr
import kotlinx.cinterop.refTo
import platform.Security.SecRandomCopyBytes
import platform.Security.errSecSuccess
import platform.Security.kSecRandomDefault
import platform.posix.CLOCK_MONOTONIC
import platform.posix.clock_gettime
import platform.posix.timespec

/** Apple implementation using the shared AES engine and the system secure random source. */
@OptIn(ExperimentalForeignApi::class)
internal actual object PlatformCrypto {
    private const val ivLength = 16

    actual fun encryptAesCbcPkcs7(data: ByteArray, password: String): ByteArray {
        val iv = ByteArray(ivLength)
        check(SecRandomCopyBytes(kSecRandomDefault, iv.size.convert(), iv.refTo(0)) == errSecSuccess) {
            "Unable to generate a cryptographically secure IV."
        }

        val ciphertext = PureAes256Cbc.encrypt(data, deriveKey(password), iv)
        return iv + ciphertext
    }

    actual fun decryptAesCbcPkcs7(encryptedData: ByteArray, password: String): ByteArray {
        require(encryptedData.size >= ivLength) { "Encrypted data is missing its IV." }
        val iv = encryptedData.copyOfRange(0, ivLength)
        val ciphertext = encryptedData.copyOfRange(ivLength, encryptedData.size)
        return PureAes256Cbc.decrypt(ciphertext, deriveKey(password), iv)
    }

    actual fun nanoTime(): Long = memScoped {
        val time = alloc<timespec>()
        check(clock_gettime(CLOCK_MONOTONIC.convert(), time.ptr) == 0) { "Unable to read monotonic time." }
        time.tv_sec.toLong() * NANOSECONDS_PER_SECOND + time.tv_nsec.toLong()
    }

    private fun deriveKey(password: String): ByteArray =
        io.github.kawase.shared.ios.MentoraSha256.digest(password.encodeToByteArray())

    private const val NANOSECONDS_PER_SECOND = 1_000_000_000L
}
