import Foundation
import SwiftUI
import MentoraShared

/// Parent learning dashboard backed exclusively by the encrypted server snapshot.
struct GoalsView: View {
    @ObservedObject var store: MentoraLiveStore
    @State private var challenge = ""
    @State private var showingNewGoal = false
    @State private var selectedInsight: ServerInsight?

    private let accent = MentoraTheme.accent
    private let machineLearningAccent = Color(red: 0.55, green: 0.36, blue: 0.96)

    private var child: Child? {
        guard let childID = store.selectedChildID else { return nil }
        return store.snapshot.children.first { $0.id == childID }
    }

    private var profile: IosChildProfile? {
        guard let childID = store.selectedChildID else { return nil }
        return store.snapshot.profiles.first { $0.childId == childID }
    }

    private var liveSession: LiveSessionState? {
        guard let childID = store.selectedChildID else { return nil }
        return store.snapshot.liveSessions.first { $0.childId == childID }
    }

    private var weeklyReport: WeeklyReport? {
        guard let childID = store.selectedChildID else { return nil }
        return store.snapshot.weeklyReports.first { $0.childId == childID }
    }

    private var profileData: ServerProfileData? {
        profile.flatMap(ServerProfileData.init)
    }

    var body: some View {
        GlassBackground(accent: accent) {
            ScrollView {
                LazyVStack(alignment: .leading, spacing: 14) {
                    MentoraPageTitle(title: "Goals", subtitle: "Set goals and rewards")
                    if let child {
                        selectedChildHeader(child)
                        liveSessionCard
                        challengeCard(for: child)
                        weeklyReportCard
                        heatmapCard
                        radarCard
                        machineLearningSection
                        insightsSection
                        goalsSection
                    } else {
                        emptyState
                    }
                }
                .padding(16)
                .padding(.bottom, 108)
            }
        }
        .navigationTitle("MENTORA")
        .navigationBarTitleDisplayMode(.inline)
        .toolbar {
            ToolbarItem(placement: .topBarTrailing) {
                Button { showingNewGoal = true } label: { Image(systemName: "plus.circle.fill") }
                    .tint(accent)
                    .accessibilityLabel("New goal")
                    .disabled(child == nil)
            }
        }
        .sheet(isPresented: $showingNewGoal) {
            if let child {
                NewGoalSheet(childID: child.id, tasks: store.snapshot.tasks, store: store)
            }
        }
        .sheet(item: $selectedInsight) { insight in
            InsightDetailSheet(insight: insight)
        }
        .task(id: store.selectedChildID) {
            if let childID = store.selectedChildID {
                store.loadChildDetails(for: childID)
            }
        }
    }

    private var emptyState: some View {
        VStack(spacing: 12) {
            Image(systemName: "person.crop.circle.badge.questionmark")
                .font(.system(size: 48))
                .foregroundStyle(accent.opacity(0.5))
            Text("Select a child").font(.title3.weight(.bold))
            Text("Choose a child from Home to see their goals and insights.")
                .font(.subheadline).foregroundStyle(.secondary)
                .multilineTextAlignment(.center)
        }
        .frame(maxWidth: .infinity, minHeight: 360)
    }

    private func selectedChildHeader(_ child: Child) -> some View {
        HStack(spacing: 12) {
            AvatarView(name: child.name, accent: accent, size: 42)
            VStack(alignment: .leading, spacing: 2) {
                Text("Learning plan for \(child.name)").font(.headline.weight(.bold))
                Text("\(child.points) points collected").font(.caption).foregroundStyle(.secondary)
            }
            Spacer()
        }
        .padding(.bottom, 2)
    }

    private var liveSessionCard: some View {
        let state = liveSession
        let isOnline = state?.isOnline == true
        return GlassCard {
            VStack(alignment: .leading, spacing: 12) {
                HStack {
                    Label("Live session", systemImage: "eye.fill").font(.headline.weight(.heavy))
                    Spacer()
                    Text(isOnline ? "Online" : "Offline")
                        .font(.caption.weight(.bold))
                        .foregroundStyle(isOnline ? MentoraTheme.success : .secondary)
                        .padding(.horizontal, 10).padding(.vertical, 5)
                        .background((isOnline ? MentoraTheme.success : Color.gray).opacity(0.13), in: Capsule())
                }
                if let state, state.isOnline {
                    HStack(spacing: 8) {
                        MentoraMetric(label: "Pad", value: state.padName.isEmpty ? "Exploring" : state.padName, tint: accent)
                        MentoraMetric(label: "Attempts", value: "\(state.attemptCount)", tint: MentoraTheme.warning)
                        MentoraMetric(label: "Hint", value: state.hasRequestedHint ? "Asked" : "No", tint: state.hasRequestedHint ? MentoraTheme.danger : MentoraTheme.success)
                    }
                    Text(state.status.isEmpty ? "Watching current activity" : state.status)
                        .font(.caption).foregroundStyle(.secondary)
                    Text(state.codeText.isEmpty ? "Code appears here" : state.codeText)
                        .font(.system(.caption, design: .monospaced))
                        .frame(maxWidth: .infinity, alignment: .leading)
                        .padding(12)
                        .background(.primary.opacity(0.06), in: RoundedRectangle(cornerRadius: 12, style: .continuous))
                } else {
                    Text("No active game feed right now.").font(.subheadline).foregroundStyle(.secondary)
                }
            }
        }
    }

    private func challengeCard(for child: Child) -> some View {
        GlassCard {
            VStack(alignment: .leading, spacing: 12) {
                Label("Tonight's challenge", systemImage: "paperplane.fill")
                    .font(.headline.weight(.heavy))
                Text("Send \(child.name) a short note in the game.").font(.caption).foregroundStyle(.secondary)
                TextField("Example: Complete one loop challenge", text: $challenge, axis: .vertical)
                    .lineLimit(2...4)
                    .textFieldStyle(.roundedBorder)
                Button {
                    store.sendChallenge(to: child.id, message: challenge)
                    challenge = ""
                } label: {
                    Label("Send to game", systemImage: "paperplane.fill")
                        .frame(maxWidth: .infinity)
                }
                .buttonStyle(.borderedProminent)
                .tint(accent)
                .disabled(challenge.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty || !store.isConnected)
            }
        }
    }

    private var weeklyReportCard: some View {
        GlassCard {
            VStack(alignment: .leading, spacing: 10) {
                HStack {
                    Label("Weekly AI report", systemImage: "sparkles").font(.headline.weight(.heavy))
                    Spacer()
                    Button {
                        if let childID = store.selectedChildID { store.loadChildDetails(for: childID) }
                    } label: { Image(systemName: "arrow.clockwise") }
                        .tint(accent)
                        .accessibilityLabel("Refresh report")
                        .disabled(!store.isConnected)
                }
                Text(weeklyReport.map { "\($0.weekStart) to \($0.weekEnd)" } ?? "Your child's weekly learning summary")
                    .font(.caption).foregroundStyle(.secondary)
                Text(weeklyReport?.reportText ?? "No report has been generated yet.")
                    .font(.subheadline)
                    .fixedSize(horizontal: false, vertical: true)
            }
        }
    }

    private var heatmapCard: some View {
        let pointsByDay = completedPointsByDay(store.snapshot.completedTasks)
        let days = (0..<56).compactMap { Calendar.current.date(byAdding: .day, value: -55 + $0, to: Date()) }
        return GlassCard {
            VStack(alignment: .leading, spacing: 12) {
                Label("Learning heatmap", systemImage: "calendar").font(.headline.weight(.heavy))
                Text("Daily points from the last eight weeks").font(.caption).foregroundStyle(.secondary)
                HStack(spacing: 4) {
                    ForEach(0..<8, id: \.self) { week in
                        VStack(spacing: 4) {
                            ForEach(0..<7, id: \.self) { day in
                                let date = days[week * 7 + day]
                                RoundedRectangle(cornerRadius: 3, style: .continuous)
                                    .fill(heatColor(for: pointsByDay[Calendar.current.startOfDay(for: date)] ?? 0))
                                    .frame(width: 15, height: 15)
                            }
                        }
                    }
                }
                HStack(spacing: 12) {
                    heatmapLegend("Low", MentoraTheme.danger)
                    heatmapLegend("Good", MentoraTheme.warning)
                    heatmapLegend("Strong", MentoraTheme.success)
                }
            }
        }
    }

    private func heatmapLegend(_ label: String, _ color: Color) -> some View {
        Label(label, systemImage: "square.fill").font(.caption2).foregroundStyle(color)
    }

    private func heatColor(for points: Int) -> Color {
        switch points {
        case ...0: return .primary.opacity(0.07)
        case 1..<15: return MentoraTheme.danger.opacity(0.72)
        case 15..<40: return MentoraTheme.warning.opacity(0.75)
        default: return MentoraTheme.success.opacity(0.80)
        }
    }

    private var radarCard: some View {
        let scores = profileData?.skillScores ?? ServerProfileData.emptyScores
        let labels = ServerProfileData.axes
        return GlassCard {
            VStack(alignment: .leading, spacing: 12) {
                Label("Skill radar", systemImage: "scope").font(.headline.weight(.heavy))
                Text("An overview of learning signals across each topic").font(.caption).foregroundStyle(.secondary)
                SkillRadarShape(accent: accent, values: labels.map { scores[$0] ?? 0 }, labels: labels)
                    .frame(height: 210)
                HStack(spacing: 8) {
                    ForEach(labels.sorted { (scores[$0] ?? 0) > (scores[$1] ?? 0) }.prefix(3), id: \.self) { label in
                        MentoraMetric(label: label, value: "\(Int((scores[label] ?? 0) * 100))%", tint: accent)
                    }
                }
            }
        }
    }

    private var insightsSection: some View {
        let insights = profileData?.insights ?? []
        return VStack(alignment: .leading, spacing: 10) {
            Text("AI insights").font(.title3.weight(.heavy))
            if insights.isEmpty {
                Text("The learning profile will appear after the child has completed activities.")
                    .font(.subheadline).foregroundStyle(.secondary)
            } else {
                ForEach(insights) { insight in
                    InsightCard(insight: insight) { selectedInsight = insight }
                }
            }
        }
    }

    @ViewBuilder
    private var machineLearningSection: some View {
        if let data = profileData,
           data.hasMachineLearningActivity,
           let insight = data.machineLearningInsight {
            VStack(alignment: .leading, spacing: 10) {
                Text("ai_machine_learning")
                    .font(.title3.weight(.heavy))
                    .foregroundStyle(machineLearningAccent)

                GlassCard {
                    VStack(alignment: .leading, spacing: 12) {
                        Label("machine_learning_radar", systemImage: "scope")
                            .font(.headline.weight(.heavy))
                            .foregroundStyle(machineLearningAccent)
                        Text("machine_learning_radar_description")
                            .font(.caption)
                            .foregroundStyle(.secondary)
                        SkillRadarShape(
                            accent: machineLearningAccent,
                            values: ServerProfileData.machineLearningAxes.map {
                                data.machineLearningScores[$0.id] ?? 0
                            },
                            labels: ServerProfileData.machineLearningAxes.map(\.label)
                        )
                        .frame(height: 210)
                        HStack(spacing: 8) {
                            ForEach(
                                ServerProfileData.machineLearningAxes.sorted {
                                    (data.machineLearningScores[$0.id] ?? 0) >
                                        (data.machineLearningScores[$1.id] ?? 0)
                                }.prefix(3),
                                id: \.id
                            ) { axis in
                                MentoraMetric(
                                    label: axis.label,
                                    value: "\(Int((data.machineLearningScores[axis.id] ?? 0) * 100))%",
                                    tint: machineLearningAccent
                                )
                            }
                        }
                    }
                }

                InsightCard(insight: insight) { selectedInsight = insight }
            }
        }
    }

    private func insightRow(_ title: String, _ items: [String], color: Color) -> some View {
        HStack(alignment: .top, spacing: 8) {
            Text(title).font(.caption.weight(.bold)).foregroundStyle(color).frame(width: 76, alignment: .leading)
            Text(items.joined(separator: ", ")).font(.caption).foregroundStyle(.secondary)
        }
    }

    private var goalsSection: some View {
        VStack(alignment: .leading, spacing: 10) {
            Text("Rewards").font(.title3.weight(.heavy))
            if store.snapshot.goals.isEmpty {
                Text("No goals are set yet.").font(.subheadline).foregroundStyle(.secondary)
            }
            ForEach(store.snapshot.goals, id: \.id) { goal in
                GlassCard(padding: 18) {
                    HStack(spacing: 12) {
                        VStack(alignment: .leading, spacing: 4) {
                            Text(goal.title).font(.headline.weight(.bold)).foregroundStyle(goal.isCompleted ? accent : .primary)
                            Label("\(goal.reward) - \(goal.requiredPoints) points", systemImage: "gift.fill")
                                .font(.caption).foregroundStyle(.secondary)
                        }
                        Spacer()
                        Image(systemName: goal.isCompleted ? "checkmark.circle.fill" : "lock.circle")
                            .font(.title2).foregroundStyle(goal.isCompleted ? accent : Color.gray.opacity(0.6))
                    }
                }
                .accessibilityElement(children: .combine)
                .accessibilityHint(goal.isCompleted ? "Completed" : "Locked until its server requirements are met")
            }
        }
    }

    private func completedPointsByDay(_ tasks: [CompletedTask]) -> [Date: Int] {
        tasks.reduce(into: [:]) { totals, task in
            guard let date = parseServerDate(task.completedAt) else { return }
            totals[Calendar.current.startOfDay(for: date), default: 0] += Int(task.pointValue)
        }
    }

    private func parseServerDate(_ value: String) -> Date? {
        ISO8601DateFormatter().date(from: value)
            ?? DateFormatter.serverTimestamp.date(from: value)
            ?? DateFormatter.serverDate.date(from: value)
    }
}

private struct NewGoalSheet: View {
    let childID: Int64
    let tasks: [MentoraShared.Task]
    @ObservedObject var store: MentoraLiveStore
    @Environment(\.dismiss) private var dismiss
    @State private var title = ""
    @State private var reward = ""
    @State private var points = "50"
    @State private var requiredTaskID: Int64 = -1
    @State private var usesPoints = true

    private var isValid: Bool {
        !title.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty
            && !reward.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty
            && (usesPoints ? (Int(points) ?? 0) > 0 : requiredTaskID != -1)
    }

    var body: some View {
        NavigationStack {
            ScrollView {
                VStack(alignment: .leading, spacing: 20) {
                    Text("Choose a reward, then decide whether it is unlocked by points or one specific task.")
                        .font(.subheadline)
                        .foregroundStyle(.secondary)

                    VStack(alignment: .leading, spacing: 10) {
                        Text("GOAL").font(.caption.weight(.bold)).foregroundStyle(.secondary)
                        TextField("Goal title", text: $title)
                            .textFieldStyle(.roundedBorder)
                        TextField("Reward", text: $reward)
                            .textFieldStyle(.roundedBorder)
                    }

                    VStack(alignment: .leading, spacing: 12) {
                        Text("HOW TO COMPLETE").font(.caption.weight(.bold)).foregroundStyle(.secondary)
                        HStack(spacing: 10) {
                            requirementButton(title: "Points", icon: "star.fill", selected: usesPoints) {
                                usesPoints = true
                            }
                            requirementButton(title: "A task", icon: "checklist", selected: !usesPoints) {
                                usesPoints = false
                            }
                        }
                        if usesPoints {
                            TextField("Points required", text: $points)
                                .textFieldStyle(.roundedBorder)
                                .keyboardType(.numberPad)
                            Text("The reward unlocks when the child reaches this total.")
                                .font(.caption).foregroundStyle(.secondary)
                        } else if tasks.isEmpty {
                            Label("No tasks have been loaded yet.", systemImage: "arrow.clockwise")
                                .font(.subheadline).foregroundStyle(.secondary)
                        } else {
                            Text("SELECT A TASK").font(.caption.weight(.bold)).foregroundStyle(.secondary)
                            LazyVStack(spacing: 8) {
                                ForEach(tasks, id: \.id) { task in
                                    Button { requiredTaskID = task.id } label: {
                                        HStack(spacing: 12) {
                                            Image(systemName: requiredTaskID == task.id ? "checkmark.circle.fill" : "circle")
                                                .font(.title3)
                                                .foregroundStyle(requiredTaskID == task.id ? MentoraTheme.accent : .secondary)
                                            VStack(alignment: .leading, spacing: 2) {
                                                Text(task.name).font(.subheadline.weight(.semibold)).foregroundStyle(.primary)
                                                Text("\(task.points) points").font(.caption).foregroundStyle(.secondary)
                                            }
                                            Spacer()
                                        }
                                        .padding(12)
                                        .background(requiredTaskID == task.id ? MentoraTheme.accent.opacity(0.13) : .primary.opacity(0.05), in: RoundedRectangle(cornerRadius: 12, style: .continuous))
                                        .overlay(RoundedRectangle(cornerRadius: 12, style: .continuous).stroke(requiredTaskID == task.id ? MentoraTheme.accent : .primary.opacity(0.08), lineWidth: 1))
                                    }
                                    .buttonStyle(.plain)
                                }
                            }
                        }
                    }
                }
                .padding(20)
            }
            .navigationTitle("New goal")
            .toolbar {
                ToolbarItem(placement: .cancellationAction) { Button("Cancel") { dismiss() } }
                ToolbarItem(placement: .confirmationAction) {
                    Button("Add") {
                        let requiredPoints = usesPoints ? Int32(points) ?? 0 : 0
                        store.addGoal(childID: childID, title: title, reward: reward, requiredPoints: requiredPoints, requiredTaskID: usesPoints ? -1 : requiredTaskID)
                        dismiss()
                    }
                    .disabled(!isValid || !store.isConnected)
                }
            }
        }
    }

    private func requirementButton(title: String, icon: String, selected: Bool, action: @escaping () -> Void) -> some View {
        Button(action: action) {
            Label(title, systemImage: icon)
                .font(.subheadline.weight(.bold))
                .frame(maxWidth: .infinity)
                .padding(.vertical, 11)
                .foregroundStyle(selected ? .white : .primary)
                .background(selected ? MentoraTheme.accent : .primary.opacity(0.06), in: RoundedRectangle(cornerRadius: 12, style: .continuous))
        }
        .buttonStyle(.plain)
        .accessibilityAddTraits(selected ? .isSelected : [])
    }
}

struct ServerInsight: Identifiable {
    let id: String
    let title: String
    let accent: Color
    let strengths: [String]
    let needsSupport: [String]
    let score: Int
    let summary: String
    let detailedSummary: String
    let level: String
    let totalInteractions: Int
    let correctCount: Int
    let incorrectCount: Int
    let hintsUsed: Int
    let chatTurns: Int
    let struggles: [String]
    let commonMistakes: [String]
    let helpTopics: [String]
    let recentMistakes: [String]
}

private struct InsightCard: View {
    let insight: ServerInsight
    let onShowDetails: () -> Void
    @State private var isExpanded = false

    var body: some View {
        GlassCard(padding: 16, cornerRadius: 20) {
            VStack(alignment: .leading, spacing: 10) {
                Button { withAnimation(.easeInOut(duration: 0.2)) { isExpanded.toggle() } } label: {
                    VStack(alignment: .leading, spacing: 8) {
                        HStack(spacing: 10) {
                            Text(insight.title)
                                .font(.headline.weight(.heavy))
                                .foregroundStyle(insight.accent)
                            Text(insight.level)
                                .font(.caption.weight(.bold))
                                .foregroundStyle(.secondary)
                            Spacer()
                            Text("\(insight.score)%")
                                .font(.headline.weight(.black))
                                .foregroundStyle(insight.accent)
                            Image(systemName: isExpanded ? "chevron.up" : "chevron.down")
                                .font(.caption.weight(.bold))
                                .foregroundStyle(.secondary)
                        }
                        Text(insight.summary)
                            .font(.caption)
                            .foregroundStyle(.secondary)
                            .lineLimit(isExpanded ? nil : 1)
                            .frame(maxWidth: .infinity, alignment: .leading)
                    }
                }
                .buttonStyle(.plain)
                .accessibilityLabel("\(insight.title) learning insight")
                .accessibilityHint(isExpanded ? "Collapse details" : "Expand details")

                if isExpanded {
                    VStack(alignment: .leading, spacing: 12) {
                        Text(insight.detailedSummary)
                            .font(.subheadline)
                            .foregroundStyle(.secondary)
                        HStack(spacing: 8) {
                            MentoraMetric(label: "Correct", value: "\(insight.correctCount)", tint: MentoraTheme.success)
                            MentoraMetric(label: "Wrong", value: "\(insight.incorrectCount)", tint: MentoraTheme.danger)
                            MentoraMetric(label: "Hints", value: "\(insight.hintsUsed)", tint: MentoraTheme.warning)
                        }
                        if !insight.strengths.isEmpty { insightRow("Strengths", insight.strengths, color: MentoraTheme.success) }
                        if !insight.needsSupport.isEmpty { insightRow("Needs help", insight.needsSupport, color: MentoraTheme.danger) }
                        Button(action: onShowDetails) {
                            Label("View full details", systemImage: "chart.bar.doc.horizontal")
                                .font(.subheadline.weight(.bold))
                                .frame(maxWidth: .infinity)
                        }
                        .buttonStyle(.bordered)
                        .tint(insight.accent)
                    }
                    .transition(.opacity.combined(with: .move(edge: .top)))
                }
            }
        }
    }

    private func insightRow(_ title: String, _ items: [String], color: Color) -> some View {
        HStack(alignment: .top, spacing: 8) {
            Text(title).font(.caption.weight(.bold)).foregroundStyle(color).frame(width: 76, alignment: .leading)
            Text(items.joined(separator: ", ")).font(.caption).foregroundStyle(.secondary)
        }
    }
}

private struct InsightDetailSheet: View {
    let insight: ServerInsight
    @Environment(\.dismiss) private var dismiss

    var body: some View {
        NavigationStack {
            ScrollView {
                VStack(alignment: .leading, spacing: 20) {
                    VStack(alignment: .leading, spacing: 6) {
                        Text("\(insight.title) profile")
                            .font(.title2.weight(.black))
                            .foregroundStyle(insight.accent)
                        Text(insight.detailedSummary)
                            .font(.subheadline)
                            .foregroundStyle(.secondary)
                    }

                    HStack(spacing: 10) {
                        detailMetric("Level", insight.level, tint: insight.accent)
                        detailMetric("Accuracy", "\(insight.score)%", tint: insight.accent)
                        detailMetric("Attempts", "\(insight.correctCount + insight.incorrectCount)", tint: .secondary)
                    }

                    detailSection("Performance") {
                        detailRow("Total interactions", "\(insight.totalInteractions)")
                        detailRow("Correct / incorrect", "\(insight.correctCount) / \(insight.incorrectCount)")
                        detailRow("Hints used", "\(insight.hintsUsed)")
                        detailRow("AI chat turns", "\(insight.chatTurns)")
                    }

                    detailList("Strengths", insight.strengths, tint: MentoraTheme.success)
                    detailList("Needs help with", insight.needsSupport, tint: MentoraTheme.danger)
                    detailList("Struggle concepts", insight.struggles, tint: MentoraTheme.warning)
                    detailList("Common mistakes", insight.commonMistakes, tint: MentoraTheme.danger)
                    detailList("Asked AI about", insight.helpTopics, tint: insight.accent)
                    detailList("Recent mistakes", insight.recentMistakes, tint: MentoraTheme.warning)
                }
                .padding(20)
            }
            .navigationTitle("Learning details")
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .confirmationAction) { Button("Done") { dismiss() } }
            }
        }
        .presentationDetents([.medium, .large])
    }

    private func detailMetric(_ label: String, _ value: String, tint: Color) -> some View {
        VStack(alignment: .leading, spacing: 3) {
            Text(value).font(.headline.weight(.black)).foregroundStyle(tint)
            Text(label).font(.caption).foregroundStyle(.secondary)
        }
        .frame(maxWidth: .infinity, alignment: .leading)
        .padding(12)
        .background(tint.opacity(0.10), in: RoundedRectangle(cornerRadius: 12, style: .continuous))
    }

    private func detailSection<Content: View>(_ title: String, @ViewBuilder content: () -> Content) -> some View {
        VStack(alignment: .leading, spacing: 8) {
            Text(title).font(.headline.weight(.bold))
            content()
        }
    }

    private func detailRow(_ label: String, _ value: String) -> some View {
        HStack {
            Text(label).font(.subheadline).foregroundStyle(.secondary)
            Spacer()
            Text(value).font(.subheadline.weight(.semibold))
        }
    }

    @ViewBuilder
    private func detailList(_ title: String, _ values: [String], tint: Color) -> some View {
        if !values.isEmpty {
            VStack(alignment: .leading, spacing: 7) {
                Text(title).font(.headline.weight(.bold)).foregroundStyle(tint)
                ForEach(values, id: \.self) { value in
                    Label(value, systemImage: "circle.fill")
                        .font(.subheadline)
                        .foregroundStyle(.secondary)
                        .symbolRenderingMode(.hierarchical)
                }
            }
        }
    }
}

struct ServerProfileData {
    struct MachineLearningAxis {
        let id: String
        let label: String
    }

    static let axes = ["Loops", "Functions", "Conditionals", "Recursion", "Memory", "Data Structures"]
    static let emptyScores = Dictionary(uniqueKeysWithValues: axes.map { ($0, CGFloat(0)) })
    static let machineLearningAxes = [
        MachineLearningAxis(id: "Data Prep", label: String(localized: "ml_axis_data_prep")),
        MachineLearningAxis(id: "Regression", label: String(localized: "ml_axis_regression")),
        MachineLearningAxis(id: "Classification", label: String(localized: "ml_axis_classification")),
        MachineLearningAxis(id: "Evaluation", label: String(localized: "ml_axis_evaluation")),
        MachineLearningAxis(id: "Neural Networks", label: String(localized: "ml_axis_neural_networks")),
        MachineLearningAxis(id: "LLMs", label: String(localized: "ml_axis_llms"))
    ]

    let insights: [ServerInsight]
    let skillScores: [String: CGFloat]
    let machineLearningInsight: ServerInsight?
    let machineLearningScores: [String: CGFloat]

    var hasMachineLearningActivity: Bool {
        guard let insight = machineLearningInsight else { return false }
        return insight.totalInteractions > 0 ||
            insight.correctCount > 0 ||
            insight.incorrectCount > 0 ||
            insight.hintsUsed > 0 ||
            insight.chatTurns > 0
    }

    init?(_ profile: IosChildProfile) {
        self.init(gameStatsJson: profile.gameStatsJson)
    }

    init?(gameStatsJson: String) {
        guard let raw = gameStatsJson.data(using: .utf8),
              let root = try? JSONSerialization.jsonObject(with: raw) as? [String: Any] else { return nil }

        var aggregate = Dictionary(uniqueKeysWithValues: Self.axes.map { ($0, (correct: 0, incorrect: 0)) })
        let sourceProfiles: [(String, Color)] = [("aiProfileCpp", .blue), ("aiProfilePython", .green), ("aiProfileGeneral", MentoraTheme.warning)]
        insights = sourceProfiles.compactMap { key, color in
            guard let profile = root[key] as? [String: Any] else { return nil }
            let correct = (profile["correctCount"] as? NSNumber)?.intValue ?? 0
            let incorrect = (profile["incorrectCount"] as? NSNumber)?.intValue ?? 0
            let total = correct + incorrect
            let topicScores = Self.topicScores(profile["topics"] as? [String: Any])
            let strengths = topicScores.filter { $0.value > 0 }.sorted { $0.value > $1.value }.prefix(3).map(\.key)
            let needsHelp = topicScores.filter { $0.value < 0 }.sorted { $0.value < $1.value }.prefix(3).map(\.key)
            let conceptScores = Self.topicScores(profile["concepts"] as? [String: Any])
            let struggles = conceptScores.filter { $0.value < 0 }.sorted { $0.value < $1.value }.prefix(3).map(\.key)
            let helpTopics = Self.helpTopics(profile["concepts"] as? [String: Any])
            let commonMistakes = Self.countedKeys(profile["mistakes"] as? [String: Any])
            let recentMistakes = Self.recentMistakes(profile["recentEvents"] as? [Any])
            for (topic, values) in Self.topicStats(profile["topics"] as? [String: Any]) {
                Self.axes.forEach { axis in
                    guard Self.matches(topic, axis: axis) else { return }
                    aggregate[axis, default: (0, 0)].correct += values.correct
                    aggregate[axis, default: (0, 0)].incorrect += values.incorrect
                }
            }
            for (concept, values) in Self.topicStats(profile["concepts"] as? [String: Any]) {
                Self.axes.forEach { axis in
                    guard Self.matches(concept, axis: axis) else { return }
                    aggregate[axis, default: (0, 0)].correct += values.correct
                    aggregate[axis, default: (0, 0)].incorrect += values.incorrect
                }
            }
            let title = key.replacingOccurrences(of: "aiProfile", with: "")
            let score = total == 0 ? 0 : Int((Double(correct) / Double(total) * 100).rounded())
            let level = (profile["level"] as? String) ?? Self.level(forTotal: total, accuracy: score)
            let summary = (profile["summaryOneLine"] as? String).flatMap { $0.isEmpty ? nil : $0 }
                ?? (total == 0 ? "No activity yet." : "\(level) level - \(score)% accuracy across \(total) attempts.")
            let detailedSummary = (profile["summaryThreeLine"] as? String).flatMap { $0.isEmpty ? nil : $0 }
                ?? (profile["summaryText"] as? String).flatMap { $0.isEmpty ? nil : $0 }
                ?? Self.detailSummary(summary: summary, strengths: strengths, needsHelp: needsHelp)
            return ServerInsight(
                id: key,
                title: title.isEmpty ? "General" : title,
                accent: color,
                strengths: strengths,
                needsSupport: needsHelp,
                score: score,
                summary: summary,
                detailedSummary: detailedSummary,
                level: level,
                totalInteractions: (profile["totalInteractions"] as? NSNumber)?.intValue ?? total,
                correctCount: correct,
                incorrectCount: incorrect,
                hintsUsed: (profile["hintsUsed"] as? NSNumber)?.intValue ?? 0,
                chatTurns: (profile["chatTurns"] as? NSNumber)?.intValue ?? 0,
                struggles: struggles,
                commonMistakes: commonMistakes,
                helpTopics: helpTopics,
                recentMistakes: recentMistakes
            )
        }
        skillScores = Dictionary(uniqueKeysWithValues: Self.axes.map { axis in
            let values = aggregate[axis] ?? (0, 0)
            let total = values.correct + values.incorrect
            return (axis, total == 0 ? 0 : CGFloat(values.correct) / CGFloat(total))
        })

        let machineLearningProfile = root["aiProfileMachineLearning"] as? [String: Any]
        machineLearningInsight = machineLearningProfile.map { Self.makeMachineLearningInsight($0) }
        machineLearningScores = Self.makeMachineLearningScores(machineLearningProfile)
    }

    private static func makeMachineLearningInsight(_ profile: [String: Any]) -> ServerInsight {
        let correct = nonNegativeInt(profile["correctCount"])
        let incorrect = nonNegativeInt(profile["incorrectCount"])
        let total = correct + incorrect
        let topicScoreByTopic = Self.topicScores(profile["topics"] as? [String: Any])
        let strengths = topicScoreByTopic
            .filter { $0.value > 0 }
            .sorted { $0.value > $1.value }
            .prefix(3)
            .map { displayMachineLearningTopic($0.key) }
        let needsHelp = topicScoreByTopic
            .filter { $0.value < 0 }
            .sorted { $0.value < $1.value }
            .prefix(3)
            .map { displayMachineLearningTopic($0.key) }
        let conceptScores = Self.topicScores(profile["concepts"] as? [String: Any])
        let struggles = conceptScores
            .filter { $0.value < 0 }
            .sorted { $0.value < $1.value }
            .prefix(3)
            .map { displayMachineLearningTopic($0.key) }
        let score = total == 0 ? 0 : Int((Double(correct) / Double(total) * 100).rounded())
        let level = level(forTotal: total, accuracy: score)
        let summary = (profile["summaryOneLine"] as? String).flatMap { $0.isEmpty ? nil : $0 }
            ?? (total == 0 ? "No activity yet." : "\(level) level - \(score)% accuracy across \(total) attempts.")
        let detailedSummary = (profile["summaryThreeLine"] as? String).flatMap { $0.isEmpty ? nil : $0 }
            ?? (profile["summaryText"] as? String).flatMap { $0.isEmpty ? nil : $0 }
            ?? detailSummary(summary: summary, strengths: strengths, needsHelp: needsHelp)
        return ServerInsight(
            id: "aiProfileMachineLearning",
            title: String(localized: "ai_machine_learning"),
            accent: Color(red: 0.55, green: 0.36, blue: 0.96),
            strengths: strengths,
            needsSupport: needsHelp,
            score: score,
            summary: summary,
            detailedSummary: detailedSummary,
            level: level,
            totalInteractions: nonNegativeInt(profile["totalInteractions"]),
            correctCount: correct,
            incorrectCount: incorrect,
            hintsUsed: nonNegativeInt(profile["hintsUsed"]),
            chatTurns: nonNegativeInt(profile["chatTurns"]),
            struggles: struggles,
            commonMistakes: countedKeys(profile["mistakes"] as? [String: Any])
                .map { displayMachineLearningTopic($0) },
            helpTopics: helpTopics(profile["concepts"] as? [String: Any])
                .map { displayMachineLearningTopic($0) },
            recentMistakes: recentMistakes(profile["recentEvents"] as? [Any])
                .map { displayMachineLearningTopic($0) }
        )
    }

    private static func makeMachineLearningScores(_ profile: [String: Any]?) -> [String: CGFloat] {
        var totals = Dictionary(
            uniqueKeysWithValues: machineLearningAxes.map { ($0.id, (correct: 0, incorrect: 0)) }
        )
        for source in [profile?["topics"] as? [String: Any], profile?["concepts"] as? [String: Any]] {
            for (topic, values) in topicStats(source) {
                guard let axis = machineLearningAxis(for: topic) else { continue }
                totals[axis, default: (0, 0)].correct += max(0, values.correct)
                totals[axis, default: (0, 0)].incorrect += max(0, values.incorrect)
            }
        }
        return Dictionary(uniqueKeysWithValues: machineLearningAxes.map { axis in
            let values = totals[axis.id] ?? (0, 0)
            let attempts = values.correct + values.incorrect
            return (axis.id, attempts == 0 ? 0 : CGFloat(values.correct) / CGFloat(attempts))
        })
    }

    private static func machineLearningAxis(for rawTopic: String) -> String? {
        let topic = rawTopic
            .lowercased()
            .replacingOccurrences(of: "_", with: " ")
            .replacingOccurrences(of: "-", with: " ")
            .replacingOccurrences(of: ":", with: " ")
            .split(whereSeparator: \.isWhitespace)
            .joined(separator: " ")
        if ["data prep", "preprocess", "data clean", "missing value", "dataset inspection"]
            .contains(where: { topic.contains($0) }) { return "Data Prep" }
        if ["neural network", "neural", "mlp"].contains(where: { topic.contains($0) }) { return "Neural Networks" }
        if ["llm", "language model", "n gram", "ngram", "tf idf", "tfidf", "intent", "next token"]
            .contains(where: { topic.contains($0) }) { return "LLMs" }
        if ["evaluation", "metric", "mean absolute", "mean squared", "mae", "mse", "r2", "r squared"]
            .contains(where: { topic.contains($0) }) { return "Evaluation" }
        if ["classification", "classifier", "logistic"].contains(where: { topic.contains($0) }) { return "Classification" }
        return topic.contains("regression") ? "Regression" : nil
    }

    private static func displayMachineLearningTopic(_ rawTopic: String) -> String {
        rawTopic
            .replacingOccurrences(of: "ml:", with: "")
            .replacingOccurrences(of: "_", with: " ")
            .replacingOccurrences(of: "-", with: " ")
            .trimmingCharacters(in: .whitespacesAndNewlines)
    }

    private static func nonNegativeInt(_ value: Any?) -> Int {
        max(0, (value as? NSNumber)?.intValue ?? 0)
    }

    private static func topicStats(_ topics: [String: Any]?) -> [String: (correct: Int, incorrect: Int)] {
        (topics ?? [:]).reduce(into: [:]) { result, item in
            guard let values = item.value as? [String: Any] else { return }
            result[item.key] = ((values["correct"] as? NSNumber)?.intValue ?? 0, (values["incorrect"] as? NSNumber)?.intValue ?? 0)
        }
    }

    private static func topicScores(_ topics: [String: Any]?) -> [String: Int] {
        topicStats(topics).mapValues { $0.correct - $0.incorrect }
    }

    private static func level(forTotal total: Int, accuracy: Int) -> String {
        guard total >= 4 else { return "Beginner" }
        if accuracy >= 85 && total >= 8 { return "Advanced" }
        return accuracy >= 65 ? "Intermediate" : "Beginner"
    }

    private static func detailSummary(summary: String, strengths: [String], needsHelp: [String]) -> String {
        var parts = [summary]
        if !strengths.isEmpty { parts.append("Strengths: \(strengths.joined(separator: ", ")).") }
        if !needsHelp.isEmpty { parts.append("Needs work on: \(needsHelp.joined(separator: ", ")).") }
        return parts.joined(separator: " ")
    }

    private static func countedKeys(_ values: [String: Any]?) -> [String] {
        (values ?? [:])
            .compactMap { key, value -> (String, Int)? in
                guard let count = (value as? NSNumber)?.intValue, count > 0 else { return nil }
                return (key, count)
            }
            .sorted { $0.1 > $1.1 }
            .prefix(3)
            .map(\.0)
    }

    private static func helpTopics(_ concepts: [String: Any]?) -> [String] {
        (concepts ?? [:])
            .compactMap { key, value -> (String, Int)? in
                guard let values = value as? [String: Any] else { return nil }
                let count = (values["helpRequests"] as? NSNumber)?.intValue ?? 0
                return count > 0 ? (key, count) : nil
            }
            .sorted { $0.1 > $1.1 }
            .prefix(3)
            .map(\.0)
    }

    private static func recentMistakes(_ events: [Any]?) -> [String] {
        (events ?? []).compactMap { item in
            guard let event = item as? [String: Any], event["correctness"] as? String == "incorrect" else { return nil }
            return event["topic"] as? String
        }
        .prefix(3)
        .map { $0 }
    }

    private static func matches(_ topic: String, axis: String) -> Bool {
        let topic = topic.lowercased()
        let markers: [String]
        switch axis {
        case "Loops": markers = ["loop", "for", "while", "range"]
        case "Functions": markers = ["function", "def ", "return"]
        case "Conditionals": markers = ["conditional", "condition", "if", "else", "switch"]
        case "Recursion": markers = ["recursion", "recursive"]
        case "Memory": markers = ["memory", "pointer", "reference", "address", "pass-by-reference"]
        default: markers = ["data", "structure", "array", "vector", "list", "collection", "dictionary", "map"]
        }
        return markers.contains { topic.contains($0) }
    }
}

private struct SkillRadarShape: View {
    let accent: Color
    let values: [CGFloat]
    let labels: [String]

    var body: some View {
        GeometryReader { proxy in
            let rect = CGRect(origin: .zero, size: proxy.size)
            let center = CGPoint(x: rect.midX, y: rect.midY)
            let radius = min(rect.width, rect.height) * 0.30
            ZStack {
                ForEach(1...4, id: \.self) { ring in
                    Polygon(sides: labels.count, scale: CGFloat(ring) / 4, center: center, radius: radius).stroke(.primary.opacity(0.12), lineWidth: 1)
                }
                ForEach(labels.indices, id: \.self) { index in
                    Path { path in path.move(to: center); path.addLine(to: point(index: index, scale: 1, center: center, radius: radius)) }.stroke(.primary.opacity(0.12), lineWidth: 1)
                }
                Polygon(sides: labels.count, individualScales: values, center: center, radius: radius)
                    .fill(accent.opacity(0.22))
                    .overlay(Polygon(sides: labels.count, individualScales: values, center: center, radius: radius).stroke(accent, lineWidth: 2))
                ForEach(labels.indices, id: \.self) { index in
                    Text(labels[index]).font(.caption2.weight(.semibold)).foregroundStyle(.secondary)
                        .position(point(index: index, scale: 1.28, center: center, radius: radius))
                }
            }
        }
    }

    private func point(index: Int, scale: CGFloat, center: CGPoint, radius: CGFloat) -> CGPoint {
        let angle = CGFloat(index) * .pi * 2 / CGFloat(labels.count) - .pi / 2
        return CGPoint(x: center.x + cos(angle) * radius * scale, y: center.y + sin(angle) * radius * scale)
    }
}

private struct Polygon: Shape {
    let sides: Int
    var scale: CGFloat = 1
    var individualScales: [CGFloat]? = nil
    let center: CGPoint
    let radius: CGFloat

    func path(in rect: CGRect) -> Path {
        var path = Path()
        for index in 0..<sides {
            let value = individualScales?[index] ?? scale
            let angle = CGFloat(index) * .pi * 2 / CGFloat(sides) - .pi / 2
            let point = CGPoint(x: center.x + cos(angle) * radius * value, y: center.y + sin(angle) * radius * value)
            index == 0 ? path.move(to: point) : path.addLine(to: point)
        }
        path.closeSubpath()
        return path
    }
}

private extension DateFormatter {
    static let serverTimestamp: DateFormatter = {
        let formatter = DateFormatter()
        formatter.locale = Locale(identifier: "en_US_POSIX")
        formatter.dateFormat = "yyyy-MM-dd HH:mm:ss"
        return formatter
    }()

    static let serverDate: DateFormatter = {
        let formatter = DateFormatter()
        formatter.locale = Locale(identifier: "en_US_POSIX")
        formatter.dateFormat = "yyyy-MM-dd"
        return formatter
    }()
}
