package io.github.kawase.shared.protocol

import java.nio.ByteBuffer
import java.nio.charset.StandardCharsets
import java.security.MessageDigest
import java.security.SecureRandom
import javax.crypto.Cipher
import javax.crypto.spec.IvParameterSpec
import javax.crypto.spec.SecretKeySpec

internal actual object PlatformCrypto {
    private const val algorithm = "AES/CBC/PKCS5Padding"
    private const val ivLength = 16

    actual fun encryptAesCbcPkcs7(data: ByteArray, password: String): ByteArray {
        val iv = ByteArray(ivLength).also(SecureRandom()::nextBytes)
        val cipher = Cipher.getInstance(algorithm)
        cipher.init(Cipher.ENCRYPT_MODE, deriveKey(password), IvParameterSpec(iv))
        val ciphertext = cipher.doFinal(data)
        return ByteBuffer.allocate(iv.size + ciphertext.size)
            .put(iv)
            .put(ciphertext)
            .array()
    }

    actual fun decryptAesCbcPkcs7(encryptedData: ByteArray, password: String): ByteArray {
        require(encryptedData.size >= ivLength) { "Encrypted data is missing its IV." }
        val iv = encryptedData.copyOfRange(0, ivLength)
        val ciphertext = encryptedData.copyOfRange(ivLength, encryptedData.size)
        val cipher = Cipher.getInstance(algorithm)
        cipher.init(Cipher.DECRYPT_MODE, deriveKey(password), IvParameterSpec(iv))
        return cipher.doFinal(ciphertext)
    }

    actual fun nanoTime(): Long = System.nanoTime()

    private fun deriveKey(password: String): SecretKeySpec {
        val key = MessageDigest.getInstance("SHA-256").digest(password.toByteArray(StandardCharsets.UTF_8))
        return SecretKeySpec(key, "AES")
    }
}
