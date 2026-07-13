import SwiftUI

private enum DashboardTab: CaseIterable {
    case home
    case history
    case goals
    case settings

    var title: String {
        switch self {
        case .home: return "Home"
        case .history: return "History"
        case .goals: return "Goals"
        case .settings: return "Settings"
        }
    }

    var icon: String {
        switch self {
        case .home: return "house.fill"
        case .history: return "list.bullet"
        case .goals: return "star.fill"
        case .settings: return "gearshape.fill"
        }
    }
}

struct DashboardView: View {
    @EnvironmentObject private var appModel: AppModel
    @StateObject private var store = MentoraPreviewStore()
    @State private var selectedTab: DashboardTab = .home
    @State private var qrChild: MentoraChild?

    var body: some View {
        ZStack(alignment: .bottom) {
            NavigationStack {
                selectedScreen
            }
            .preferredColorScheme(store.isDarkMode ? .dark : nil)

            floatingTabBar
                .padding(.horizontal, 24)
                .padding(.bottom, 18)
        }
        .sheet(item: $qrChild) { child in
            QRLoginSheet(child: child)
        }
    }

    @ViewBuilder
    private var selectedScreen: some View {
        switch selectedTab {
        case .home:
            HomeView(store: store, onOpenGoals: {
                selectedTab = .goals
            }, onRequestGameLogin: { child in
                qrChild = child
            })
        case .history:
            HistoryView(store: store)
        case .goals:
            GoalsView(store: store)
        case .settings:
            SettingsView(store: store, onSignOut: {
                appModel.signOut()
            })
        }
    }

    private var floatingTabBar: some View {
        HStack(spacing: 4) {
            ForEach(DashboardTab.allCases, id: \.title) { tab in
                Button {
                    guard tab == .home || store.selectedChildID != nil else { return }
                    selectedTab = tab
                } label: {
                    VStack(spacing: 4) {
                        Image(systemName: tab.icon)
                            .font(.system(size: 18, weight: .bold))
                        Text(tab.title)
                            .font(.caption2.weight(.bold))
                    }
                    .frame(maxWidth: .infinity)
                    .foregroundStyle(selectedTab == tab ? store.accent : .secondary)
                    .padding(.vertical, 10)
                }
                .buttonStyle(.plain)
                .accessibilityHint(tab == .home || store.selectedChildID != nil ? "Open \(tab.title)" : "Select a child first")
            }
        }
        .padding(8)
        .background(.ultraThinMaterial, in: Capsule())
        .overlay(Capsule().strokeBorder(.primary.opacity(0.10), lineWidth: 1))
        .shadow(color: .black.opacity(0.14), radius: 16, y: 8)
    }
}

private struct QRLoginSheet: View {
    let child: MentoraChild
    @Environment(\.dismiss) private var dismiss
    @State private var token = ""

    var body: some View {
        NavigationStack {
            VStack(spacing: 20) {
                Image(systemName: "qrcode.viewfinder")
                    .font(.system(size: 58, weight: .semibold))
                    .foregroundStyle(MentoraTheme.accent)
                Text("Log in \(child.name) to the game")
                    .font(.title3.weight(.heavy))
                Text("Camera QR scanning will be enabled when the shared production transport is connected. You can still enter a game token manually.")
                    .font(.subheadline)
                    .foregroundStyle(.secondary)
                    .multilineTextAlignment(.center)
                TextField("Game token", text: $token)
                    .textInputAutocapitalization(.never)
                    .textFieldStyle(.roundedBorder)
                Button("Log in") {
                    dismiss()
                }
                .buttonStyle(.borderedProminent)
                .disabled(token.isEmpty)
                Spacer()
            }
            .padding(24)
            .navigationTitle("QR game login")
            .toolbar { ToolbarItem(placement: .topBarTrailing) { Button("Cancel") { dismiss() } } }
        }
    }
}
