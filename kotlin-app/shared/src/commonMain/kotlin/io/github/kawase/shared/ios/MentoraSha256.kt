package io.github.kawase.shared.ios

/**
 * Small, dependency-free SHA-256 implementation for the credential hashes used by
 * the existing Java server. Keeping it in common code makes Android and iOS send
 * identical lower-case hexadecimal hashes without exposing a platform API to Swift.
 */
object MentoraSha256 {
    private val roundConstants = intArrayOf(
        0x428a2f98, 0x71374491, 0xb5c0fbcf.toInt(), 0xe9b5dba5.toInt(),
        0x3956c25b, 0x59f111f1, 0x923f82a4.toInt(), 0xab1c5ed5.toInt(),
        0xd807aa98.toInt(), 0x12835b01, 0x243185be, 0x550c7dc3,
        0x72be5d74, 0x80deb1fe.toInt(), 0x9bdc06a7.toInt(), 0xc19bf174.toInt(),
        0xe49b69c1.toInt(), 0xefbe4786.toInt(), 0x0fc19dc6, 0x240ca1cc,
        0x2de92c6f, 0x4a7484aa, 0x5cb0a9dc, 0x76f988da,
        0x983e5152.toInt(), 0xa831c66d.toInt(), 0xb00327c8.toInt(), 0xbf597fc7.toInt(),
        0xc6e00bf3.toInt(), 0xd5a79147.toInt(), 0x06ca6351, 0x14292967,
        0x27b70a85, 0x2e1b2138, 0x4d2c6dfc, 0x53380d13,
        0x650a7354, 0x766a0abb, 0x81c2c92e.toInt(), 0x92722c85.toInt(),
        0xa2bfe8a1.toInt(), 0xa81a664b.toInt(), 0xc24b8b70.toInt(), 0xc76c51a3.toInt(),
        0xd192e819.toInt(), 0xd6990624.toInt(), 0xf40e3585.toInt(), 0x106aa070,
        0x19a4c116, 0x1e376c08, 0x2748774c, 0x34b0bcb5,
        0x391c0cb3, 0x4ed8aa4a, 0x5b9cca4f, 0x682e6ff3,
        0x748f82ee, 0x78a5636f, 0x84c87814.toInt(), 0x8cc70208.toInt(),
        0x90befffa.toInt(), 0xa4506ceb.toInt(), 0xbef9a3f7.toInt(), 0xc67178f2.toInt()
    )

    fun hex(input: String): String = hex(input.encodeToByteArray())

    fun hex(input: ByteArray): String {
        val digest = digest(input)
        val result = StringBuilder(digest.size * 2)
        for (byte in digest) {
            result.append(HEX[(byte.toInt() ushr 4) and 0x0F])
            result.append(HEX[byte.toInt() and 0x0F])
        }
        return result.toString()
    }

    fun digest(input: ByteArray): ByteArray {
        val padded = pad(input)
        var a = 0x6a09e667
        var b = 0xbb67ae85.toInt()
        var c = 0x3c6ef372
        var d = 0xa54ff53a.toInt()
        var e = 0x510e527f
        var f = 0x9b05688c.toInt()
        var g = 0x1f83d9ab
        var h = 0x5be0cd19

        val words = IntArray(64)
        var chunkOffset = 0
        while (chunkOffset < padded.size) {
            for (index in 0 until 16) {
                val offset = chunkOffset + index * 4
                words[index] = ((padded[offset].toInt() and 0xFF) shl 24) or
                    ((padded[offset + 1].toInt() and 0xFF) shl 16) or
                    ((padded[offset + 2].toInt() and 0xFF) shl 8) or
                    (padded[offset + 3].toInt() and 0xFF)
            }
            for (index in 16 until 64) {
                val s0 = words[index - 15].rotateRight(7) xor words[index - 15].rotateRight(18) xor (words[index - 15] ushr 3)
                val s1 = words[index - 2].rotateRight(17) xor words[index - 2].rotateRight(19) xor (words[index - 2] ushr 10)
                words[index] = words[index - 16] + s0 + words[index - 7] + s1
            }

            var aa = a
            var bb = b
            var cc = c
            var dd = d
            var ee = e
            var ff = f
            var gg = g
            var hh = h
            for (index in 0 until 64) {
                val s1 = ee.rotateRight(6) xor ee.rotateRight(11) xor ee.rotateRight(25)
                val choice = (ee and ff) xor (ee.inv() and gg)
                val temporary1 = hh + s1 + choice + roundConstants[index] + words[index]
                val s0 = aa.rotateRight(2) xor aa.rotateRight(13) xor aa.rotateRight(22)
                val majority = (aa and bb) xor (aa and cc) xor (bb and cc)
                val temporary2 = s0 + majority
                hh = gg
                gg = ff
                ff = ee
                ee = dd + temporary1
                dd = cc
                cc = bb
                bb = aa
                aa = temporary1 + temporary2
            }
            a += aa
            b += bb
            c += cc
            d += dd
            e += ee
            f += ff
            g += gg
            h += hh
            chunkOffset += 64
        }

        return byteArrayOfWords(a, b, c, d, e, f, g, h)
    }

    private fun pad(input: ByteArray): ByteArray {
        val bitLength = input.size.toLong() * 8L
        val zeroCount = (64 - ((input.size + 1 + 8) % 64)) % 64
        return ByteArray(input.size + 1 + zeroCount + 8).also { output ->
            input.copyInto(output)
            output[input.size] = 0x80.toByte()
            for (index in 0 until 8) {
                output[output.lastIndex - index] = (bitLength ushr (index * 8)).toByte()
            }
        }
    }

    private fun byteArrayOfWords(vararg words: Int): ByteArray = ByteArray(words.size * 4).also { bytes ->
        words.forEachIndexed { index, word ->
            val offset = index * 4
            bytes[offset] = (word ushr 24).toByte()
            bytes[offset + 1] = (word ushr 16).toByte()
            bytes[offset + 2] = (word ushr 8).toByte()
            bytes[offset + 3] = word.toByte()
        }
    }

    private const val HEX = "0123456789abcdef"
}
