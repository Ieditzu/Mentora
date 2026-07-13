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
    @AppStorage("mentora.darkMode") private var isDarkMode = false
    @State private var selectedTab: DashboardTab = .home
    @State private var qrChild: QRLoginTarget?

    private var store: MentoraLiveStore { appModel.liveStore }

    var body: some View {
        ZStack(alignment: .bottom) {
            NavigationStack {
                selectedScreen
            }
            .preferredColorScheme(isDarkMode ? .dark : nil)

            floatingTabBar
                .padding(.horizontal, 24)
                .padding(.bottom, 18)
        }
        .sheet(item: $qrChild) { child in
            QRLoginSheet(child: child) { token in
                store.claimQRLogin(token: token, for: child.id)
            }
        }
        .onChange(of: appModel.resolvedLanguageTag) { languageTag in
            store.setLanguage(languageTag)
        }
        .onAppear {
            store.setLanguage(appModel.resolvedLanguageTag)
        }
    }

    @ViewBuilder
    private var selectedScreen: some View {
        switch selectedTab {
        case .home:
            HomeView(store: store, onOpenGoals: {
                selectedTab = .goals
            }, onRequestGameLogin: { childID, childName in
                qrChild = QRLoginTarget(id: childID, name: childName)
            })
        case .history:
            HistoryView(store: store)
        case .goals:
            GoalsView(store: store)
        case .settings:
            SettingsView(store: store, onSignOut: {
                store.disconnect()
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
                    .foregroundStyle(selectedTab == tab ? MentoraTheme.accent : .secondary)
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

private struct QRLoginTarget: Identifiable {
    let id: Int64
    let name: String
}

private struct QRLoginSheet: View {
    let child: QRLoginTarget
    let onLogin: (String) -> Void
    @Environment(\.dismiss) private var dismiss
    @State private var token = ""
    @State private var showsScanner = false

    var body: some View {
        NavigationStack {
            VStack(spacing: 20) {
                Image(systemName: "qrcode.viewfinder")
                    .font(.system(size: 58, weight: .semibold))
                    .foregroundStyle(MentoraTheme.accent)
                Text("Log in \(child.name) to the game")
                    .font(.title3.weight(.heavy))
                Text("Enter the token shown in the game to connect this child's session.")
                    .font(.subheadline)
                    .foregroundStyle(.secondary)
                    .multilineTextAlignment(.center)
                TextField("Game token", text: $token)
                    .textInputAutocapitalization(.never)
                    .textFieldStyle(.roundedBorder)
                Button {
                    showsScanner = true
                } label: {
                    Label("Scan QR code", systemImage: "camera.viewfinder")
                }
                .buttonStyle(.bordered)
                Button("Log in") {
                    onLogin(token)
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
        .sheet(isPresented: $showsScanner) {
            QRCodeScannerSheet { scannedToken in
                onLogin(scannedToken)
                dismiss()
            }
        }
    }
}
