import SwiftUI
import MentoraShared

struct HistoryView: View {
    @ObservedObject var store: MentoraLiveStore

    private var calendar: Calendar { .current }
    private var history: [HistoryEntry] {
        store.snapshot.completedTasks.map {
            HistoryEntry(id: $0.id, title: $0.taskTitle, points: Int($0.pointValue), completedAt: parseDate($0.completedAt))
        }
    }

    private var groupedHistory: [(date: Date, entries: [HistoryEntry])] {
        let groups = Dictionary(grouping: history) { calendar.startOfDay(for: $0.completedAt) }
        return groups.map { ($0.key, $0.value.sorted { $0.completedAt > $1.completedAt }) }
            .sorted { $0.date > $1.date }
    }

    var body: some View {
        GlassBackground(accent: MentoraTheme.accent) {
            ScrollView {
                LazyVStack(alignment: .leading, spacing: 18) {
                    MentoraPageTitle(title: "Task history", subtitle: "Completed tasks from the game")
                    if history.isEmpty {
                        VStack(spacing: 12) {
                            Image(systemName: "checkmark.circle")
                                .font(.system(size: 48))
                                .foregroundStyle(MentoraTheme.accent.opacity(0.5))
                            Text("No completed tasks").font(.title3.weight(.bold))
                            Text("Completed learning tasks will appear here.")
                                .font(.subheadline).foregroundStyle(.secondary)
                        }
                        .frame(maxWidth: .infinity, minHeight: 360)
                    } else {
                        summary
                        ForEach(groupedHistory, id: \.date) { group in
                            section(for: group.date, entries: group.entries)
                        }
                    }
                }
                .padding(16)
                .padding(.bottom, 108)
            }
        }
        .navigationTitle("MENTORA")
        .navigationBarTitleDisplayMode(.inline)
    }

    private var summary: some View {
        HStack(spacing: 10) {
            MentoraMetric(label: "Total", value: "\(history.count)", tint: MentoraTheme.accent)
            MentoraMetric(label: "Points", value: "\(history.reduce(0) { $0 + $1.points })", tint: MentoraTheme.success)
            MentoraMetric(label: "Days", value: "\(groupedHistory.count)", tint: MentoraTheme.warning)
        }
    }

    private func section(for date: Date, entries: [HistoryEntry]) -> some View {
        VStack(alignment: .leading, spacing: 8) {
            HStack(spacing: 8) {
                Text(date, format: .dateTime.weekday(.wide).month(.abbreviated).day())
                    .font(.subheadline.weight(.bold))
                    .foregroundStyle(MentoraTheme.accent)
                Rectangle().fill(.primary.opacity(0.12)).frame(height: 1)
                Text("\(entries.count) tasks, \(entries.reduce(0) { $0 + $1.points }) pts")
                    .font(.caption.weight(.medium))
                    .foregroundStyle(.secondary)
            }
            ForEach(entries) { entry in
                GlassCard(padding: 14, cornerRadius: 16) {
                    HStack(spacing: 12) {
                        Image(systemName: "checkmark.circle.fill")
                            .font(.title3)
                            .foregroundStyle(MentoraTheme.accent)
                            .frame(width: 34, height: 34)
                            .background(MentoraTheme.accent.opacity(0.12), in: Circle())
                        VStack(alignment: .leading, spacing: 3) {
                            Text(entry.title).font(.subheadline.weight(.bold)).foregroundStyle(.primary)
                            HStack(spacing: 8) {
                                Text("+\(entry.points) pts").foregroundStyle(MentoraTheme.accent).fontWeight(.black)
                                Text(entry.completedAt, format: .dateTime.hour().minute())
                                    .foregroundStyle(.secondary)
                            }
                            .font(.caption)
                        }
                        Spacer()
                    }
                }
            }
        }
    }

    private func parseDate(_ value: String) -> Date {
        let iso8601 = ISO8601DateFormatter()
        if let date = iso8601.date(from: value) { return date }
        let formatter = DateFormatter()
        formatter.locale = Locale(identifier: "en_US_POSIX")
        formatter.timeZone = .current
        for format in ["yyyy-MM-dd HH:mm:ss", "yyyy-MM-dd'T'HH:mm:ss", "yyyy-MM-dd"] {
            formatter.dateFormat = format
            if let date = formatter.date(from: value) { return date }
        }
        return .distantPast
    }
}

private struct HistoryEntry: Identifiable {
    let id: Int64
    let title: String
    let points: Int
    let completedAt: Date
}
