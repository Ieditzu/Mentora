import SwiftUI

/// Reusable full-screen QR scanning sheet with a clear manual-entry fallback.
///
/// Use `onToken` to pass the scanned string to the encrypted Mentora transport. This
/// component deliberately has no networking dependency, so it can be reused for every
/// token-based login flow.
struct QRCodeScannerSheet: View {
    let onToken: (String) -> Void

    @Environment(\.dismiss) private var dismiss
    @State private var failure: QRCodeCameraScannerFailure?
    @State private var manualToken = ""
    @State private var showsManualEntry = false

    var body: some View {
        NavigationStack {
            ZStack(alignment: .bottom) {
                QRCodeCameraScanner(
                    onScan: { token in
                        onToken(token)
                        dismiss()
                    },
                    onFailure: { failure in
                        self.failure = failure
                    }
                )
                .ignoresSafeArea(edges: .bottom)

                scannerOverlay
            }
            .navigationTitle("Scan game QR code")
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .topBarLeading) {
                    Button("Cancel") { dismiss() }
                }
                ToolbarItem(placement: .topBarTrailing) {
                    Button("Enter token") { showsManualEntry = true }
                }
            }
            .alert(
                "Camera unavailable",
                isPresented: Binding(
                    get: { failure != nil },
                    set: { if !$0 { failure = nil } }
                ),
                presenting: failure
            ) { _ in
                Button("Enter token") { showsManualEntry = true }
                Button("Cancel", role: .cancel) { dismiss() }
            } message: { failure in
                Text(failure.localizedDescription)
            }
            .sheet(isPresented: $showsManualEntry) {
                manualEntry
            }
        }
    }

    private var scannerOverlay: some View {
        VStack(spacing: 12) {
            RoundedRectangle(cornerRadius: 22, style: .continuous)
                .stroke(.white, lineWidth: 3)
                .frame(width: 250, height: 250)
                .shadow(color: .black.opacity(0.35), radius: 8)
            Text("Place the game QR code inside the frame")
                .font(.subheadline.weight(.semibold))
                .foregroundStyle(.white)
                .padding(.horizontal, 18)
                .padding(.vertical, 10)
                .background(.black.opacity(0.58), in: Capsule())
        }
        .padding(.bottom, 48)
        .accessibilityElement(children: .combine)
        .accessibilityLabel("QR code camera scanner")
        .accessibilityHint("Point the camera at the game QR code")
    }

    private var manualEntry: some View {
        NavigationStack {
            Form {
                Section("Game token") {
                    TextField("Token", text: $manualToken)
                        .textInputAutocapitalization(.never)
                        .autocorrectionDisabled()
                }
            }
            .navigationTitle("Enter token")
            .toolbar {
                ToolbarItem(placement: .cancellationAction) {
                    Button("Cancel") { showsManualEntry = false }
                }
                ToolbarItem(placement: .confirmationAction) {
                    Button("Use token") {
                        let token = manualToken.trimmingCharacters(in: .whitespacesAndNewlines)
                        guard !token.isEmpty else { return }
                        onToken(token)
                        showsManualEntry = false
                        dismiss()
                    }
                    .disabled(manualToken.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty)
                }
            }
        }
    }
}
