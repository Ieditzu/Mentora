import SwiftUI

struct ContentView: View {
    @EnvironmentObject private var appModel: AppModel

    var body: some View {
        Group {
            if appModel.isAuthenticated {
                DashboardView()
            } else {
                AuthenticationView()
            }
        }
    }
}

private struct AuthenticationView: View {
    @EnvironmentObject private var appModel: AppModel
    @State private var email = ""
    @State private var password = ""
    @State private var isRegistering = false
    @State private var glowOffset = false

    private var store: MentoraLiveStore { appModel.liveStore }
    private var isSubmitting: Bool { store.authenticationState.isPending }

    var body: some View {
        ZStack {
            Color(uiColor: .systemBackground).ignoresSafeArea()
            Circle()
                .fill(MentoraTheme.accent.opacity(0.40))
                .frame(width: 520, height: 520)
                .blur(radius: 80)
                .offset(x: glowOffset ? 180 : -180, y: glowOffset ? -250 : -90)
            Circle()
                .fill(MentoraTheme.secondary.opacity(0.30))
                .frame(width: 460, height: 460)
                .blur(radius: 90)
                .offset(x: glowOffset ? -160 : 180, y: glowOffset ? 310 : 180)

            ScrollView {
                VStack(spacing: 0) {
                    Spacer(minLength: 42)
                    Image(systemName: "graduationcap.fill")
                        .font(.system(size: 76, weight: .black))
                        .foregroundStyle(MentoraTheme.accent)
                        .frame(width: 150, height: 150)
                        .background(.thinMaterial, in: RoundedRectangle(cornerRadius: 40, style: .continuous))
                        .overlay {
                            RoundedRectangle(cornerRadius: 40, style: .continuous)
                                .strokeBorder(.white.opacity(0.18), lineWidth: 1)
                        }
                        .shadow(color: MentoraTheme.accent.opacity(0.30), radius: 28, y: 14)

                    VStack(spacing: 6) {
                        Text("app_name")
                            .font(.system(size: 36, weight: .black, design: .rounded))
                        Text("parent_learning_companion")
                            .font(.subheadline.weight(.medium))
                            .foregroundStyle(.secondary)
                    }
                    .padding(.top, 28)

                    GlassCard(padding: 28, cornerRadius: 32) {
                        VStack(spacing: 0) {
                            Text(isRegistering ? "Create account" : "Login")
                                .font(.title2.weight(.bold))
                                .frame(maxWidth: .infinity, alignment: .leading)

                            VStack(spacing: 18) {
                                authField(icon: "envelope.fill", isEmail: true) {
                                    TextField("email_address", text: $email)
                                }
                                authField(icon: "lock.fill", isEmail: false) {
                                    SecureField("password", text: $password)
                                }
                            }
                            .padding(.top, 28)

                            Button(action: submit) {
                                HStack(spacing: 10) {
                                    if isSubmitting {
                                        ProgressView().tint(.white)
                                    }
                                    Text(buttonTitle).font(.headline.weight(.heavy))
                                }
                                .frame(maxWidth: .infinity)
                                .frame(height: 60)
                            }
                            .buttonStyle(.plain)
                            .foregroundStyle(.white)
                            .background(MentoraTheme.accent, in: RoundedRectangle(cornerRadius: 20, style: .continuous))
                            .shadow(color: MentoraTheme.accent.opacity(0.38), radius: 12, y: 7)
                            .padding(.top, 34)
                            .disabled(email.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty || password.isEmpty || isSubmitting)
                            .opacity(email.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty || password.isEmpty ? 0.55 : 1)

                            Button(isRegistering ? "I already have an account" : "Create a parent account") {
                                guard !isSubmitting else { return }
                                isRegistering.toggle()
                            }
                            .font(.subheadline.weight(.semibold))
                            .foregroundStyle(MentoraTheme.accent)
                            .padding(.top, 16)
                        }
                    }
                    .padding(.top, 38)

                    connectionStatus
                        .padding(.top, 22)
                    if let error = store.lastErrorMessage {
                        Text(error)
                            .font(.footnote)
                            .foregroundStyle(MentoraTheme.danger)
                            .multilineTextAlignment(.center)
                            .padding(.top, 12)
                    }
                    Spacer(minLength: 42)
                }
                .padding(.horizontal, 24)
            }
        }
        .animation(.easeInOut(duration: 1.2), value: glowOffset)
        .onAppear { glowOffset = true }
        .preferredColorScheme(nil)
    }

    private var buttonTitle: String {
        switch store.authenticationState {
        case .waitingForConnection: return "Connecting…"
        case .signingIn: return "Signing in…"
        case .creatingAccount: return "Creating account…"
        case .idle: return isRegistering ? "Register" : "Login"
        }
    }

    private var connectionStatus: some View {
        VStack(spacing: 8) {
            if !store.isConnected || store.authenticationState == .waitingForConnection {
                ProgressView().tint(MentoraTheme.accent)
                Text(store.connectionState == .reconnecting ? "Reconnecting securely…" : "Connecting securely…")
                    .font(.caption)
                    .foregroundStyle(.secondary)
            } else {
                Label("Secure server connection ready", systemImage: "checkmark.circle.fill")
                    .font(.caption.weight(.medium))
                    .foregroundStyle(MentoraTheme.success)
            }
        }
    }

    private func authField<Content: View>(icon: String, isEmail: Bool, @ViewBuilder content: () -> Content) -> some View {
        HStack(spacing: 12) {
            Image(systemName: icon)
                .font(.subheadline.weight(.bold))
                .foregroundStyle(MentoraTheme.accent)
                .frame(width: 20)
            content()
                .textInputAutocapitalization(.never)
                .autocorrectionDisabled()
                .textContentType(isEmail ? .emailAddress : .password)
        }
        .padding(.horizontal, 16)
        .frame(height: 58)
        .background(.primary.opacity(0.045), in: RoundedRectangle(cornerRadius: 16, style: .continuous))
        .overlay {
            RoundedRectangle(cornerRadius: 16, style: .continuous)
                .strokeBorder(.primary.opacity(0.10), lineWidth: 1)
        }
    }

    private func submit() {
        if isRegistering {
            appModel.register(email: email, password: password)
        } else {
            appModel.login(email: email, password: password)
        }
    }
}
