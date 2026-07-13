import SwiftUI

struct HomeView: View {
    @ObservedObject var store: MentoraPreviewStore
    var onOpenGoals: (() -> Void)?
    var onRequestGameLogin: ((MentoraChild) -> Void)?

    var body: some View {
        GlassBackground(accent: store.accent) {
            ScrollView {
                LazyVStack(alignment: .leading, spacing: 20) {
                    MentoraPageTitle(title: "My kids", subtitle: "Monitor their learning progress")
                        .padding(.bottom, 8)

                    if store.children.isEmpty {
                        emptyState
                    } else {
                        ForEach(store.children) { child in
                            childCard(child)
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

    private var emptyState: some View {
        VStack(spacing: 14) {
            Image(systemName: "face.smiling")
                .font(.system(size: 56, weight: .semibold))
                .foregroundStyle(store.accent.opacity(0.45))
            Text("No kids added yet")
                .font(.title3.weight(.bold))
            Text("Add your first child in Settings to start following their progress.")
                .font(.subheadline)
                .foregroundStyle(.secondary)
                .multilineTextAlignment(.center)
        }
        .frame(maxWidth: .infinity)
        .padding(.vertical, 80)
    }

    private func childCard(_ child: MentoraChild) -> some View {
        Button {
            store.select(child)
            onOpenGoals?()
        } label: {
            GlassCard(padding: 20) {
                HStack(spacing: 16) {
                    AvatarView(name: child.name, accent: store.accent, size: 60)
                    VStack(alignment: .leading, spacing: 5) {
                        HStack(spacing: 7) {
                            Text(child.name)
                                .font(.title3.weight(.heavy))
                                .foregroundStyle(.primary)
                            if child.isOnline {
                                Circle().fill(MentoraTheme.success).frame(width: 8, height: 8)
                                    .accessibilityLabel("Online")
                            }
                        }
                        Label("\(child.points) points", systemImage: "star.fill")
                            .font(.subheadline)
                            .foregroundStyle(store.accent)
                    }
                    Spacer(minLength: 8)
                    Button {
                        onRequestGameLogin?(child)
                    } label: {
                        Image(systemName: child.isOnline ? "desktopcomputer" : "qrcode.viewfinder")
                            .font(.headline)
                            .foregroundStyle(child.isOnline ? MentoraTheme.danger : store.accent)
                            .frame(width: 44, height: 44)
                            .background((child.isOnline ? MentoraTheme.danger : store.accent).opacity(0.12), in: Circle())
                    }
                    .buttonStyle(.plain)
                    Image(systemName: "chevron.right")
                        .font(.subheadline.weight(.bold))
                        .foregroundStyle(.tertiary)
                }
            }
        }
        .buttonStyle(.plain)
        .accessibilityHint("Opens goals and learning insights")
    }
}
