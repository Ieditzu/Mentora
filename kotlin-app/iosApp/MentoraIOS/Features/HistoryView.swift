import SwiftUI

struct HistoryView: View {
    @ObservedObject var store: MentoraPreviewStore

    private var calendar: Calendar { .current }
    private var groupedHistory: [(date: Date, entries: [MentoraHistoryEntry])] {
        let groups = Dictionary(grouping: store.history) { calendar.startOfDay(for: $0.completedAt) }
        return groups.map { ($0.key, $0.value.sorted { $0.completedAt > $1.completedAt }) }
            .sorted { $0.date > $1.date }
    }

    var body: some View {
        GlassBackground(accent: store.accent) {
            ScrollView {
                LazyVStack(alignment: .leading, spacing: 18) {
                    MentoraPageTitle(title: "Task history", subtitle: "Completed tasks from the game")
                    if store.history.isEmpty {
                        VStack(spacing: 12) {
                            Image(systemName: "checkmark.circle")
                                .font(.system(size: 48))
                                .foregroundStyle(store.accent.opacity(0.5))
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
                .padding(24)
                .padding(.bottom, 108)
            }
        }
        .navigationTitle("MENTORA")
        .navigationBarTitleDisplayMode(.inline)
    }

    private var summary: some View {
        HStack(spacing: 10) {
            MentoraMetric(label: "Total", value: "\(store.history.count)", tint: store.accent)
            MentoraMetric(label: "Points", value: "\(store.history.reduce(0) { $0 + $1.points })", tint: MentoraTheme.success)
            MentoraMetric(label: "Days", value: "\(groupedHistory.count)", tint: MentoraTheme.warning)
        }
    }

    private func section(for date: Date, entries: [MentoraHistoryEntry]) -> some View {
        VStack(alignment: .leading, spacing: 8) {
            HStack(spacing: 8) {
                Text(date, format: .dateTime.weekday(.wide).month(.abbreviated).day())
                    .font(.subheadline.weight(.bold))
                    .foregroundStyle(store.accent)
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
                            .foregroundStyle(store.accent)
                            .frame(width: 34, height: 34)
                            .background(store.accent.opacity(0.12), in: Circle())
                        VStack(alignment: .leading, spacing: 3) {
                            Text(entry.title).font(.subheadline.weight(.bold)).foregroundStyle(.primary)
                            HStack(spacing: 8) {
                                Text("+\(entry.points) pts").foregroundStyle(store.accent).fontWeight(.black)
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
}
