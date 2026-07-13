package io.github.kawase.shared.protocol

import kotlinx.cinterop.COpaquePointer
import kotlinx.cinterop.addressOf
import kotlinx.cinterop.alloc
import kotlinx.cinterop.convert
import kotlinx.cinterop.memScoped
import kotlinx.cinterop.ptr
import kotlinx.cinterop.size_tVar
import kotlinx.cinterop.usePinned
import platform.CommonCrypto.CCCrypt
import platform.CommonCrypto.CC_SHA256
import platform.CommonCrypto.kCCAlgorithmAES
import platform.CommonCrypto.kCCBlockSizeAES128
import platform.CommonCrypto.kCCDecrypt
import platform.CommonCrypto.kCCEncrypt
import platform.CommonCrypto.kCCKeySizeAES256
import platform.CommonCrypto.kCCOptionPKCS7Padding
import platform.CommonCrypto.kCCSuccess
import platform.Security.SecRandomCopyBytes
import platform.Security.errSecSuccess
import platform.Security.kSecRandomDefault
import platform.posix.CLOCK_MONOTONIC
import platform.posix.clock_gettime
import platform.posix.timespec

/** Apple implementation backed only by the system CommonCrypto and Security frameworks. */
internal actual object PlatformCrypto {
    private const val ivLength = kCCBlockSizeAES128

    actual fun encryptAesCbcPkcs7(data: ByteArray, password: String): ByteArray {
        val iv = ByteArray(ivLength)
        check(SecRandomCopyBytes(kSecRandomDefault, iv.size.convert(), iv.refTo(0)) == errSecSuccess) {
            "Unable to generate a cryptographically secure IV."
        }

        val ciphertext = crypt(kCCEncrypt, data, deriveKey(password), iv)
        return iv + ciphertext
    }

    actual fun decryptAesCbcPkcs7(encryptedData: ByteArray, password: String): ByteArray {
        require(encryptedData.size >= ivLength) { "Encrypted data is missing its IV." }
        val iv = encryptedData.copyOfRange(0, ivLength)
        val ciphertext = encryptedData.copyOfRange(ivLength, encryptedData.size)
        return crypt(kCCDecrypt, ciphertext, deriveKey(password), iv)
    }

    actual fun nanoTime(): Long = memScoped {
        val time = alloc<timespec>()
        check(clock_gettime(CLOCK_MONOTONIC, time.ptr) == 0) { "Unable to read monotonic time." }
        time.tv_sec.toLong() * NANOSECONDS_PER_SECOND + time.tv_nsec.toLong()
    }

    private fun deriveKey(password: String): ByteArray {
        val passwordBytes = password.encodeToByteArray()
        val key = ByteArray(kCCKeySizeAES256)
        passwordBytes.withPointer { passwordPointer ->
            key.usePinned { keyPinned ->
                CC_SHA256(passwordPointer, passwordBytes.size.convert(), keyPinned.addressOf(0))
            }
        }
        return key
    }

    private fun crypt(operation: UInt, input: ByteArray, key: ByteArray, iv: ByteArray): ByteArray = memScoped {
        val output = ByteArray(input.size + kCCBlockSizeAES128)
        val outputLength = alloc<size_tVar>()
        val status = key.withPointer { keyPointer ->
            iv.withPointer { ivPointer ->
                input.withPointer { inputPointer ->
                    output.usePinned { outputPinned ->
                        CCCrypt(
                            operation,
                            kCCAlgorithmAES,
                            kCCOptionPKCS7Padding,
                            keyPointer,
                            key.size.convert(),
                            ivPointer,
                            inputPointer,
                            input.size.convert(),
                            outputPinned.addressOf(0),
                            output.size.convert(),
                            outputLength.ptr
                        )
                    }
                }
            }
        }
        check(status == kCCSuccess) { "AES-CBC operation failed with status $status." }
        output.copyOf(outputLength.value.toInt())
    }

    private inline fun <T> ByteArray.withPointer(block: (COpaquePointer?) -> T): T {
        return if (isEmpty()) {
            block(null)
        } else {
            usePinned { pinned -> block(pinned.addressOf(0)) }
        }
    }

    private const val NANOSECONDS_PER_SECOND = 1_000_000_000L
}
