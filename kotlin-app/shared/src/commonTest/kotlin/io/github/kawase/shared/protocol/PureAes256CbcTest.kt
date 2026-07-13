package io.github.kawase.shared.protocol

import kotlin.test.Test
import kotlin.test.assertContentEquals
import kotlin.test.assertEquals

class PureAes256CbcTest {
    @Test
    fun `encrypts the NIST AES-256 CBC vector before its PKCS7 final block`() {
        val key = hex("603deb1015ca71be2b73aef0857d77811f352c073b6108d72d9810a30914dff4")
        val iv = hex("000102030405060708090a0b0c0d0e0f")
        val plaintext = hex(
            "6bc1bee22e409f96e93d7e117393172a" +
                "ae2d8a571e03ac9c9eb76fac45af8e51" +
                "30c81c46a35ce411e5fbc1191a0a52ef" +
                "f69f2445df4f9b17ad2b417be66c3710"
        )
        val expectedNistBlocks = hex(
            "f58c4c04d6e5f1ba779eabfb5f7bfbd6" +
                "9cfc4e967edb808d679f777bc6702c7d" +
                "39f23369a9d9bacfa530e26304231461" +
                "b2eb05e2c39be9fcda6c19078c6a9d1b"
        )

        val encrypted = PureAes256Cbc.encrypt(plaintext, key, iv)

        assertEquals(plaintext.size + 16, encrypted.size)
        assertContentEquals(expectedNistBlocks, encrypted.copyOf(plaintext.size))
        assertContentEquals(plaintext, PureAes256Cbc.decrypt(encrypted, key, iv))
    }

    private fun hex(value: String): ByteArray = ByteArray(value.length / 2) { index ->
        value.substring(index * 2, index * 2 + 2).toInt(16).toByte()
    }
}
