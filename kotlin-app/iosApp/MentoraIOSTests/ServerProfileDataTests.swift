import XCTest
@testable import MentoraIOS

final class ServerProfileDataTests: XCTestCase {
    func testMachineLearningScoresUsePerAxisAccuracy() throws {
        let data = try XCTUnwrap(ServerProfileData(gameStatsJson: """
        {
          "aiProfileMachineLearning": {
            "totalInteractions": 24,
            "correctCount": 17,
            "incorrectCount": 7,
            "topics": {
              "ml:data-prep": {"correct": 3, "incorrect": 1},
              "ml:regression": {"correct": 2, "incorrect": 2},
              "ml:classification": {"correct": 4, "incorrect": 1},
              "ml:evaluation": {"correct": 1, "incorrect": 3},
              "ml:neural-networks": {"correct": 5, "incorrect": 0},
              "ml:llm": {"correct": 2, "incorrect": 3}
            },
            "concepts": {
              "data_cleaning": {"correct": 1, "incorrect": 0},
              "linear-regression": {"correct": 1, "incorrect": 1},
              "logistic_classifier": {"correct": 0, "incorrect": 1},
              "mae_metric": {"correct": 1, "incorrect": 0},
              "mlp": {"correct": 0, "incorrect": 1},
              "next-token": {"correct": 1, "incorrect": 0}
            }
          }
        }
        """))

        XCTAssertTrue(data.hasMachineLearningActivity)
        XCTAssertEqual(data.machineLearningScores["Data Prep"] ?? -1, 0.8, accuracy: 0.0001)
        XCTAssertEqual(data.machineLearningScores["Regression"] ?? -1, 0.5, accuracy: 0.0001)
        XCTAssertEqual(data.machineLearningScores["Classification"] ?? -1, 2.0 / 3.0, accuracy: 0.0001)
        XCTAssertEqual(data.machineLearningScores["Evaluation"] ?? -1, 0.4, accuracy: 0.0001)
        XCTAssertEqual(data.machineLearningScores["Neural Networks"] ?? -1, 5.0 / 6.0, accuracy: 0.0001)
        XCTAssertEqual(data.machineLearningScores["LLMs"] ?? -1, 0.5, accuracy: 0.0001)
    }

    func testMalformedMachineLearningProfileDoesNotAffectLegacyProfiles() throws {
        let data = try XCTUnwrap(ServerProfileData(gameStatsJson: """
        {
          "aiProfileCpp": {"correctCount": 2, "incorrectCount": 1},
          "aiProfileMachineLearning": "invalid"
        }
        """))

        XCTAssertEqual(data.insights.map(\.id), ["aiProfileCpp"])
        XCTAssertNil(data.machineLearningInsight)
        XCTAssertFalse(data.hasMachineLearningActivity)
    }

    func testEmptyMachineLearningProfileStaysHidden() throws {
        let data = try XCTUnwrap(
            ServerProfileData(gameStatsJson: "{\"aiProfileMachineLearning\":{}}")
        )

        XCTAssertNotNil(data.machineLearningInsight)
        XCTAssertFalse(data.hasMachineLearningActivity)
        XCTAssertTrue(data.machineLearningScores.values.allSatisfy { $0 == 0 })
    }
}
