import XCTest
@testable import MentoraIOS

final class AuthenticationTransitionTests: XCTestCase {
    func testSignInUsesNormalizedHashBeforeLegacyExactInputHash() {
        var attempt = MentoraAuthenticationAttempt(
            emailInput: " Parent@Example.COM ",
            password: "secret",
            mode: .signIn,
            hash: { "hash<\($0)>" }
        )

        XCTAssertEqual(attempt.normalizedEmail, "parent@example.com")
        XCTAssertEqual(attempt.currentEmailHash, "hash<parent@example.com>")
        XCTAssertEqual(
            attempt.retryWithLegacyEmailHash(),
            "hash< Parent@Example.COM >"
        )
        XCTAssertEqual(attempt.currentEmailHash, "hash< Parent@Example.COM >")
        XCTAssertNil(attempt.retryWithLegacyEmailHash())

        attempt.clear()

        XCTAssertEqual(attempt.currentEmailHash, "")
        XCTAssertEqual(attempt.passwordHash, "")
    }

    func testRegistrationAndAlreadyNormalizedSignInNeverRetry() {
        var registration = MentoraAuthenticationAttempt(
            emailInput: " Parent@Example.COM ",
            password: "secret",
            mode: .register,
            hash: { "hash<\($0)>" }
        )
        var normalizedSignIn = MentoraAuthenticationAttempt(
            emailInput: "parent@example.com",
            password: "secret",
            mode: .signIn,
            hash: { "hash<\($0)>" }
        )

        XCTAssertNil(registration.retryWithLegacyEmailHash())
        XCTAssertNil(normalizedSignIn.retryWithLegacyEmailHash())
    }

    func testInvalidResumeClearsStateAndChallengeCancelReconnects() {
        XCTAssertTrue(
            MentoraAuthenticationTransition.shouldClearAuthenticatedState(
                eventType: "parentSession",
                requestPacketID: -1
            )
        )
        XCTAssertTrue(
            MentoraAuthenticationTransition.shouldClearAuthenticatedState(
                eventType: "action",
                requestPacketID: 91
            )
        )
        XCTAssertFalse(
            MentoraAuthenticationTransition.shouldClearAuthenticatedState(
                eventType: "action",
                requestPacketID: 82
            )
        )
        XCTAssertTrue(
            MentoraAuthenticationTransition.shouldReconnectAfterChallengeCancellation(
                hadChallenge: true
            )
        )
        XCTAssertFalse(
            MentoraAuthenticationTransition.shouldReconnectAfterChallengeCancellation(
                hadChallenge: false
            )
        )
    }
}
