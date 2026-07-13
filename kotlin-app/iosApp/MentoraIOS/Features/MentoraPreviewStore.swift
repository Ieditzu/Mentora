import SwiftUI

struct MentoraChild: Identifiable, Hashable {
    let id: UUID
    var name: String
    var points: Int
    var isOnline: Bool

    init(id: UUID = UUID(), name: String, points: Int, isOnline: Bool) {
        self.id = id
        self.name = name
        self.points = points
        self.isOnline = isOnline
    }
}

struct MentoraHistoryEntry: Identifiable, Hashable {
    let id = UUID()
    let title: String
    let points: Int
    let completedAt: Date
}

struct MentoraGoal: Identifiable, Hashable {
    let id = UUID()
    var title: String
    var reward: String
    var points: Int
    var isComplete: Bool
}

struct MentoraInsight: Identifiable, Hashable {
    let id = UUID()
    let title: String
    let accent: Color
    let strengths: [String]
    let needsSupport: [String]
    let score: Int
}

struct MentoraLanguage: Identifiable, Hashable {
    let tag: String
    let nativeName: String
    var id: String { tag }
}

/// Temporary UI state that mirrors the Android dashboard shape without coupling
/// these views to the transport implementation. Replace it with shared KMP state
/// once the iOS socket client is connected.
@MainActor
final class MentoraPreviewStore: ObservableObject {
    @Published var children: [MentoraChild] = [
        MentoraChild(name: "Mara", points: 285, isOnline: true),
        MentoraChild(name: "Victor", points: 160, isOnline: false)
    ]
    @Published var selectedChildID: UUID?
    @Published var history: [MentoraHistoryEntry] = MentoraPreviewStore.sampleHistory
    @Published var goals: [MentoraGoal] = [
        MentoraGoal(title: "Finish the loops lesson", reward: "Extra game time", points: 50, isComplete: true),
        MentoraGoal(title: "Solve two Python challenges", reward: "Choose Friday's movie", points: 75, isComplete: false),
        MentoraGoal(title: "Ask the AI for a hint", reward: "New game avatar", points: 30, isComplete: false)
    ]
    @Published var selectedLanguage = "system"
    @Published var isDarkMode = false
    @Published var accent: Color = MentoraTheme.accent
    @Published var parentEmail = "parent@mentora.app"
    @Published var lastChallenge = ""

    let languages: [MentoraLanguage] = [
        MentoraLanguage(tag: "system", nativeName: "Use device language"),
        MentoraLanguage(tag: "en", nativeName: "English"),
        MentoraLanguage(tag: "ro", nativeName: "Română"),
        MentoraLanguage(tag: "es", nativeName: "Español"),
        MentoraLanguage(tag: "fr", nativeName: "Français"),
        MentoraLanguage(tag: "de", nativeName: "Deutsch"),
        MentoraLanguage(tag: "it", nativeName: "Italiano"),
        MentoraLanguage(tag: "pt-BR", nativeName: "Português (Brasil)"),
        MentoraLanguage(tag: "pl", nativeName: "Polski"),
        MentoraLanguage(tag: "tr", nativeName: "Türkçe"),
        MentoraLanguage(tag: "uk", nativeName: "Українська")
    ]

    var selectedChild: MentoraChild? {
        children.first { $0.id == selectedChildID } ?? children.first
    }

    func select(_ child: MentoraChild) { selectedChildID = child.id }

    func toggle(_ goal: MentoraGoal) {
        guard let index = goals.firstIndex(where: { $0.id == goal.id }) else { return }
        goals[index].isComplete.toggle()
    }

    func addChild(named name: String) {
        let trimmed = name.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !trimmed.isEmpty else { return }
        children.append(MentoraChild(name: trimmed, points: 0, isOnline: false))
    }

    func remove(_ child: MentoraChild) {
        children.removeAll { $0.id == child.id }
        if selectedChildID == child.id { selectedChildID = children.first?.id }
    }

    private static let sampleHistory: [MentoraHistoryEntry] = {
        let calendar = Calendar.current
        let now = Date()
        return [
            MentoraHistoryEntry(title: "Build a for loop", points: 25, completedAt: calendar.date(byAdding: .hour, value: -2, to: now) ?? now),
            MentoraHistoryEntry(title: "Fix the spaceship function", points: 40, completedAt: calendar.date(byAdding: .hour, value: -3, to: now) ?? now),
            MentoraHistoryEntry(title: "Complete conditionals", points: 30, completedAt: calendar.date(byAdding: .day, value: -1, to: now) ?? now),
            MentoraHistoryEntry(title: "Try a Python list", points: 20, completedAt: calendar.date(byAdding: .day, value: -2, to: now) ?? now),
            MentoraHistoryEntry(title: "Use a helper function", points: 35, completedAt: calendar.date(byAdding: .day, value: -2, to: now) ?? now)
        ]
    }()
}
