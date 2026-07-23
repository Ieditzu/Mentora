import Foundation
import MentoraShared
import XCTest
@testable import MentoraIOS

final class ProtocolGoldenFixtureTests: XCTestCase {
    func testSharedBridgeDecodesCanonicalServerFrames() throws {
        let fixtureURL = try XCTUnwrap(
            Bundle(for: Self.self).url(forResource: "packets", withExtension: "json")
        )
        let fixture = try XCTUnwrap(
            JSONSerialization.jsonObject(with: Data(contentsOf: fixtureURL)) as? [String: Any]
        )
        let vectors = try XCTUnwrap(fixture["vectors"] as? [[String: Any]])
        XCTAssertEqual(vectors.count, 24)
        let expectedEventTypes: [Int: String] = [
            10: "authentication",
            16: "children",
            65: "liveSession",
            81: "secondFactorRequired",
            84: "totpEnrollmentDetails",
            86: "totpEnrollmentResult",
            89: "parentSecurityStatus",
            90: "parentSession",
        ]
        let bridge = IosMentoraClientBridge(languageTag: "en")
        var verifiedPacketIDs = Set<Int>()

        for vector in vectors {
            let packetID = try XCTUnwrap(vector["packetId"] as? Int)
            guard let expectedEventType = expectedEventTypes[packetID] else { continue }
            verifiedPacketIDs.insert(packetID)
            let encodedEnvelope = try XCTUnwrap(vector["encryptedEnvelopeBase64"] as? String)
            let envelope = try XCTUnwrap(Data(base64Encoded: encodedEnvelope))

            let event = bridge.receive(frame: envelope.mentoraKotlinByteArray())

            XCTAssertEqual(event.type, expectedEventType, vector["name"] as? String ?? "")
        }

        XCTAssertEqual(verifiedPacketIDs, Set(expectedEventTypes.keys))
        XCTAssertTrue(bridge.snapshot().isLoggedIn)
        XCTAssertEqual(bridge.takeSessionToken(), "parent-session-token-0001")
        XCTAssertTrue(bridge.snapshot().twoFactorEnabled)
    }
}
