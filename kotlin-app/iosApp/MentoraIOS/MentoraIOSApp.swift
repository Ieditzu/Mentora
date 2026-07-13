import SwiftUI

@main
struct MentoraIOSApp: App {
    @StateObject private var appModel = AppModel()
    @Environment(\.scenePhase) private var scenePhase

    var body: some Scene {
        WindowGroup {
            ContentView()
                .environmentObject(appModel)
                .environment(\.locale, Locale(identifier: appModel.resolvedLanguageTag))
                .onChange(of: scenePhase) { phase in
                    if phase == .active {
                        appModel.refreshDeviceLanguage()
                    }
                }
        }
    }
}
