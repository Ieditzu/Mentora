import SwiftUI

struct GoalsView: View {
    @ObservedObject var store: MentoraPreviewStore
    @State private var challenge = ""
    @State private var showingNewGoal = false
    @State private var newGoalTitle = ""

    private var insights: [MentoraInsight] {
        [
            MentoraInsight(title: "Python", accent: .green, strengths: ["Lists", "Functions"], needsSupport: ["Nested conditionals"], score: 78),
            MentoraInsight(title: "C++", accent: .blue, strengths: ["Loops", "Variables"], needsSupport: ["Memory"], score: 64),
            MentoraInsight(title: "General", accent: MentoraTheme.warning, strengths: ["Persistence", "Problem solving"], needsSupport: ["Reading errors"], score: 72)
        ]
    }

    var body: some View {
        GlassBackground(accent: store.accent) {
            ScrollView {
                LazyVStack(alignment: .leading, spacing: 14) {
                    MentoraPageTitle(title: "Goals", subtitle: "Set goals and rewards")
                    if let child = store.selectedChild {
                        selectedChildHeader(child)
                        liveSessionCard
                        challengeCard(for: child)
                        weeklyReportCard
                        heatmapCard
                        radarCard
                        insightsSection
                        goalsSection
                    } else {
                        VStack(spacing: 12) {
                            Image(systemName: "person.crop.circle.badge.questionmark")
                                .font(.system(size: 48))
                                .foregroundStyle(store.accent.opacity(0.5))
                            Text("Select a child").font(.title3.weight(.bold))
                            Text("Choose a child from Home to see their goals and insights.")
                                .font(.subheadline).foregroundStyle(.secondary)
                                .multilineTextAlignment(.center)
                        }
                            .frame(maxWidth: .infinity, minHeight: 360)
                    }
                }
                .padding(24)
                .padding(.bottom, 108)
            }
        }
        .navigationTitle("MENTORA")
        .navigationBarTitleDisplayMode(.inline)
        .toolbar {
            ToolbarItem(placement: .topBarTrailing) {
                Button { showingNewGoal = true } label: { Image(systemName: "plus.circle.fill") }
                    .tint(store.accent)
                    .accessibilityLabel("New goal")
            }
        }
        .alert("New goal", isPresented: $showingNewGoal) {
            TextField("Goal title", text: $newGoalTitle)
            Button("Add") {
                let title = newGoalTitle.trimmingCharacters(in: .whitespacesAndNewlines)
                guard !title.isEmpty else { return }
                store.goals.append(MentoraGoal(title: title, reward: "A special reward", points: 25, isComplete: false))
                newGoalTitle = ""
            }
            Button("Cancel", role: .cancel) { newGoalTitle = "" }
        }
    }

    private func selectedChildHeader(_ child: MentoraChild) -> some View {
        HStack(spacing: 12) {
            AvatarView(name: child.name, accent: store.accent, size: 42)
            VStack(alignment: .leading, spacing: 2) {
                Text("Learning plan for \(child.name)").font(.headline.weight(.bold))
                Text("\(child.points) points collected").font(.caption).foregroundStyle(.secondary)
            }
            Spacer()
        }
        .padding(.bottom, 2)
    }

    private var liveSessionCard: some View {
        GlassCard {
            VStack(alignment: .leading, spacing: 12) {
                HStack {
                    Label("Live session", systemImage: "eye.fill").font(.headline.weight(.heavy)).foregroundStyle(.primary)
                    Spacer()
                    Text(store.selectedChild?.isOnline == true ? "Online" : "Offline")
                        .font(.caption.weight(.bold))
                        .foregroundStyle(store.selectedChild?.isOnline == true ? MentoraTheme.success : .secondary)
                        .padding(.horizontal, 10).padding(.vertical, 5)
                        .background((store.selectedChild?.isOnline == true ? MentoraTheme.success : Color.gray).opacity(0.13), in: Capsule())
                }
                if store.selectedChild?.isOnline == true {
                    HStack(spacing: 8) {
                        MentoraMetric(label: "Pad", value: "Loops", tint: store.accent)
                        MentoraMetric(label: "Attempts", value: "2", tint: MentoraTheme.warning)
                        MentoraMetric(label: "Hint", value: "No", tint: MentoraTheme.success)
                    }
                    Text("Mara is working through a repeat-until challenge.").font(.caption).foregroundStyle(.secondary)
                    Text("for item in planets {\n    explore(item)\n}")
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

    private func challengeCard(for child: MentoraChild) -> some View {
        GlassCard {
            VStack(alignment: .leading, spacing: 12) {
                Label("Tonight's challenge", systemImage: "paperplane.fill")
                    .font(.headline.weight(.heavy))
                Text("Send \(child.name) a short note in the game.").font(.caption).foregroundStyle(.secondary)
                TextField("Example: Complete one loop challenge", text: $challenge, axis: .vertical)
                    .lineLimit(2...4)
                    .textFieldStyle(.roundedBorder)
                Button {
                    store.lastChallenge = challenge
                    challenge = ""
                } label: {
                    Label("Send to game", systemImage: "paperplane.fill")
                        .frame(maxWidth: .infinity)
                }
                .buttonStyle(.borderedProminent)
                .tint(store.accent)
                .disabled(challenge.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty)
            }
        }
    }

    private var weeklyReportCard: some View {
        GlassCard {
            VStack(alignment: .leading, spacing: 10) {
                HStack {
                    Label("Weekly AI report", systemImage: "sparkles").font(.headline.weight(.heavy))
                    Spacer()
                    Button(action: {}) { Image(systemName: "arrow.clockwise") }
                        .tint(store.accent)
                        .accessibilityLabel("Refresh report")
                }
                Text("This week")
                    .font(.caption).foregroundStyle(.secondary)
                Text("Mara is becoming more confident with loops and functions. A little more practice reading conditionals will help her solve longer challenges independently.")
                    .font(.subheadline)
                    .fixedSize(horizontal: false, vertical: true)
            }
        }
    }

    private var heatmapCard: some View {
        GlassCard {
            VStack(alignment: .leading, spacing: 12) {
                Label("Learning heatmap", systemImage: "calendar").font(.headline.weight(.heavy))
                Text("Daily points from the last eight weeks").font(.caption).foregroundStyle(.secondary)
                HStack(spacing: 4) {
                    ForEach(0..<8, id: \.self) { week in
                        VStack(spacing: 4) {
                            ForEach(0..<7, id: \.self) { day in
                                RoundedRectangle(cornerRadius: 3, style: .continuous)
                                    .fill(heatColor(for: week, day: day))
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
        Label(label, systemImage: "square.fill")
            .font(.caption2).foregroundStyle(color)
    }

    private func heatColor(for week: Int, day: Int) -> Color {
        let value = (week * 3 + day * 2) % 6
        switch value {
        case 0: return .primary.opacity(0.07)
        case 1, 2: return MentoraTheme.danger.opacity(0.72)
        case 3, 4: return MentoraTheme.warning.opacity(0.75)
        default: return MentoraTheme.success.opacity(0.80)
        }
    }

    private var radarCard: some View {
        GlassCard {
            VStack(alignment: .leading, spacing: 12) {
                Label("Skill radar", systemImage: "scope").font(.headline.weight(.heavy))
                Text("An overview of learning signals across each topic").font(.caption).foregroundStyle(.secondary)
                SkillRadarShape(accent: store.accent)
                    .frame(height: 210)
                HStack(spacing: 8) {
                    MentoraMetric(label: "Loops", value: "86%", tint: store.accent)
                    MentoraMetric(label: "Functions", value: "78%", tint: store.accent)
                    MentoraMetric(label: "Logic", value: "72%", tint: store.accent)
                }
            }
        }
    }

    private var insightsSection: some View {
        VStack(alignment: .leading, spacing: 10) {
            Text("AI insights").font(.title3.weight(.heavy))
            ForEach(insights) { insight in
                GlassCard(padding: 16, cornerRadius: 20) {
                    VStack(alignment: .leading, spacing: 10) {
                        HStack {
                            Text(insight.title).font(.headline.weight(.heavy)).foregroundStyle(insight.accent)
                            Spacer()
                            Text("\(insight.score)%").font(.headline.weight(.black)).foregroundStyle(insight.accent)
                        }
                        insightRow("Strengths", insight.strengths, color: MentoraTheme.success)
                        insightRow("Needs help", insight.needsSupport, color: MentoraTheme.danger)
                    }
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

    private var goalsSection: some View {
        VStack(alignment: .leading, spacing: 10) {
            Text("Rewards").font(.title3.weight(.heavy))
            ForEach(store.goals) { goal in
                Button { store.toggle(goal) } label: {
                    GlassCard(padding: 18) {
                        HStack(spacing: 12) {
                            VStack(alignment: .leading, spacing: 4) {
                                Text(goal.title).font(.headline.weight(.bold)).foregroundStyle(goal.isComplete ? store.accent : .primary)
                                Label("\(goal.reward) - \(goal.points) points", systemImage: "gift.fill")
                                    .font(.caption).foregroundStyle(.secondary)
                            }
                            Spacer()
                            Image(systemName: goal.isComplete ? "checkmark.circle.fill" : "lock.circle")
                                .font(.title2).foregroundStyle(goal.isComplete ? store.accent : Color.gray.opacity(0.6))
                        }
                    }
                }
                .buttonStyle(.plain)
                .accessibilityHint("Marks this goal as \(goal.isComplete ? "incomplete" : "complete")")
            }
        }
    }
}

private struct SkillRadarShape: View {
    let accent: Color
    private let values: [CGFloat] = [0.86, 0.78, 0.65, 0.48, 0.52, 0.72]
    private let labels = ["Loops", "Functions", "Logic", "Recursion", "Memory", "Data"]

    var body: some View {
        GeometryReader { proxy in
            let rect = CGRect(origin: .zero, size: proxy.size)
            let center = CGPoint(x: rect.midX, y: rect.midY)
            let radius = min(rect.width, rect.height) * 0.30
            ZStack {
                ForEach(1...4, id: \.self) { ring in
                    Polygon(sides: 6, scale: CGFloat(ring) / 4, center: center, radius: radius)
                        .stroke(.primary.opacity(0.12), lineWidth: 1)
                }
                ForEach(0..<6, id: \.self) { index in
                    Path { path in
                        path.move(to: center)
                        path.addLine(to: point(index: index, scale: 1, center: center, radius: radius))
                    }
                    .stroke(.primary.opacity(0.12), lineWidth: 1)
                }
                Polygon(sides: 6, individualScales: values, center: center, radius: radius)
                    .fill(accent.opacity(0.22))
                    .overlay(Polygon(sides: 6, individualScales: values, center: center, radius: radius).stroke(accent, lineWidth: 2))
                ForEach(labels.indices, id: \.self) { index in
                    Text(labels[index]).font(.caption2.weight(.semibold)).foregroundStyle(.secondary)
                        .position(point(index: index, scale: 1.28, center: center, radius: radius))
                }
            }
        }
    }

    private func point(index: Int, scale: CGFloat, center: CGPoint, radius: CGFloat) -> CGPoint {
        let angle = CGFloat(index) * .pi * 2 / 6 - .pi / 2
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
