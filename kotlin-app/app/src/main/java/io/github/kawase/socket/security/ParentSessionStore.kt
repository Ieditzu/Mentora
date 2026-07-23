package io.github.kawase.socket.security

import android.content.Context
import android.content.SharedPreferences
import android.security.keystore.KeyGenParameterSpec
import android.security.keystore.KeyProperties
import android.util.Base64
import java.security.KeyStore
import java.util.UUID
import javax.crypto.Cipher
import javax.crypto.KeyGenerator
import javax.crypto.SecretKey
import javax.crypto.spec.GCMParameterSpec

class ParentSessionStore(context: Context) {
    private val preferences: SharedPreferences = context.getSharedPreferences(
        "mentora_secure_parent_session",
        Context.MODE_PRIVATE
    )

    fun deviceId(): String {
        readEncrypted("device_id")?.let { return it }
        return UUID.randomUUID().toString().also { writeEncrypted("device_id", it) }
    }

    fun loadSessionToken(): String? = readEncrypted("session_token")

    fun saveSessionToken(sessionToken: String) {
        require(sessionToken.isNotBlank()) { "A parent session token cannot be blank." }
        writeEncrypted("session_token", sessionToken)
    }

    fun clearSessionToken() {
        requireCommitted(
            preferences.edit()
            .remove("session_token_ciphertext")
            .remove("session_token_iv")
            .commit(),
            "clear the encrypted parent session"
        )
    }

    private fun readEncrypted(name: String): String? {
        val encodedCiphertext = preferences.getString("${name}_ciphertext", null)
        val encodedIv = preferences.getString("${name}_iv", null)
        if (encodedCiphertext == null && encodedIv == null) return null
        if (encodedCiphertext == null || encodedIv == null) {
            clearEncryptedValue(name)
            throw IllegalStateException("Secure $name data was incomplete and has been cleared.")
        }
        return try {
            val cipher = Cipher.getInstance("AES/GCM/NoPadding")
            cipher.init(
                Cipher.DECRYPT_MODE,
                encryptionKey(),
                GCMParameterSpec(128, Base64.decode(encodedIv, Base64.NO_WRAP))
            )
            cipher.doFinal(Base64.decode(encodedCiphertext, Base64.NO_WRAP))
                .decodeToString()
                .takeIf(String::isNotBlank)
                ?: throw IllegalStateException("Secure $name data was empty.")
        } catch (exception: Exception) {
            runCatching { clearEncryptedValue(name) }
                .getOrElse {
                    throw IllegalStateException(
                        "Unable to clear unreadable secure parent session data.",
                        exception
                    )
                }
            throw IllegalStateException(
                "Secure $name data was unreadable and has been cleared.",
                exception
            )
        }
    }

    private fun writeEncrypted(name: String, value: String) {
        val cipher = Cipher.getInstance("AES/GCM/NoPadding")
        cipher.init(Cipher.ENCRYPT_MODE, encryptionKey())
        requireCommitted(
            preferences.edit()
            .putString(
                "${name}_ciphertext",
                Base64.encodeToString(cipher.doFinal(value.encodeToByteArray()), Base64.NO_WRAP)
            )
            .putString("${name}_iv", Base64.encodeToString(cipher.iv, Base64.NO_WRAP))
            .commit(),
            "save encrypted $name"
        )
    }

    private fun clearEncryptedValue(name: String) {
        requireCommitted(
            preferences.edit()
                .remove("${name}_ciphertext")
                .remove("${name}_iv")
                .commit(),
            "clear encrypted $name"
        )
    }

    private fun encryptionKey(): SecretKey {
        val keyStore = KeyStore.getInstance("AndroidKeyStore").apply { load(null) }
        (keyStore.getKey(KEY_ALIAS, null) as? SecretKey)?.let { return it }

        return KeyGenerator.getInstance(KeyProperties.KEY_ALGORITHM_AES, "AndroidKeyStore").run {
            init(
                KeyGenParameterSpec.Builder(
                    KEY_ALIAS,
                    KeyProperties.PURPOSE_ENCRYPT or KeyProperties.PURPOSE_DECRYPT
                )
                    .setBlockModes(KeyProperties.BLOCK_MODE_GCM)
                    .setEncryptionPaddings(KeyProperties.ENCRYPTION_PADDING_NONE)
                    .setRandomizedEncryptionRequired(true)
                    .build()
            )
            generateKey()
        }
    }

    private companion object {
        const val KEY_ALIAS = "mentora_parent_session_v1"
    }

    private fun requireCommitted(committed: Boolean, operation: String) {
        check(committed) { "Unable to durably $operation." }
    }
}
