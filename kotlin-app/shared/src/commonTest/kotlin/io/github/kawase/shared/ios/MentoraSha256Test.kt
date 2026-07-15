package io.github.kawase.shared.ios

import kotlin.test.Test
import kotlin.test.assertEquals

class MentoraSha256Test {
    @Test
    fun `matches standard SHA-256 vectors`() {
        assertEquals(
            "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
            MentoraSha256.hex("")
        )
        assertEquals(
            "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad",
            MentoraSha256.hex("abc")
        )
        assertEquals(
            "990a541de8656576eee8c8fc008a54dfcdea64682f2406bbfe2151b8f4e91e78",
            MentoraSha256.hex("CIOCLIKESKIDSIJIJSDJ1J2313J8123869699696")
        )
    }
}
