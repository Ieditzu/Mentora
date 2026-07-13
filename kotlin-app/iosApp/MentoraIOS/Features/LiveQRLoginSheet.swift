import SwiftUI

/// Manual fallback for game QR login. The server owns token validation and session creation;
/// this view only captures the token and submits it through the live encrypted transport.
struct LiveQRLoginSheet: View {
    let childID: Int64
    let childName: String
    @ObservedObject var store: MentoraLiveStore
    @Environment(\.dismiss) private var dismiss
    @State private var token = ""

    var body: some View {
        NavigationStack {
            VStack(spacing: 20) {
                Image(systemName: "qrcode.viewfinder")
                    .font(.system(size: 58, weight: .semibold))
                    .foregroundStyle(MentoraTheme.accent)
                Text("Log in \(childName) to the game")
                    .font(.title3.weight(.heavy))
                Text("Enter the token shown with the game's QR code to link this child.")
                    .font(.subheadline)
                    .foregroundStyle(.secondary)
                    .multilineTextAlignment(.center)
                TextField("Game token", text: $token)
                    .textInputAutocapitalization(.never)
                    .autocorrectionDisabled()
                    .textFieldStyle(.roundedBorder)
                Button("Log in") {
                    store.claimQRLogin(token: token, for: childID)
                    dismiss()
                }
                .buttonStyle(.borderedProminent)
                .disabled(token.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty || !store.isConnected)
                if !store.isConnected {
                    Text("Connect to the Mentora server before claiming a game login.")
                        .font(.caption)
                        .foregroundStyle(.secondary)
                        .multilineTextAlignment(.center)
                }
                Spacer()
            }
            .padding(24)
            .navigationTitle("QR game login")
            .toolbar { ToolbarItem(placement: .topBarTrailing) { Button("Cancel") { dismiss() } } }
        }
    }
}
