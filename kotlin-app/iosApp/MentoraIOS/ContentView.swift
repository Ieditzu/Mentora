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

    var body: some View {
        NavigationStack {
            VStack(spacing: 24) {
                Spacer()
                Image(systemName: "graduationcap.fill")
                    .font(.system(size: 64))
                    .foregroundStyle(.indigo)
                Text("app_name")
                    .font(.largeTitle.bold())
                Text("parent_learning_companion")
                    .foregroundStyle(.secondary)

                VStack(spacing: 12) {
                    TextField("email_address", text: $email)
                        .textInputAutocapitalization(.never)
                        .keyboardType(.emailAddress)
                        .textContentType(.emailAddress)
                        .textFieldStyle(.roundedBorder)
                    SecureField("password", text: $password)
                        .textContentType(.password)
                        .textFieldStyle(.roundedBorder)
                    Button(isRegistering ? "Create account" : "continue") {
                        if isRegistering {
                            appModel.register(email: email, password: password)
                        } else {
                            appModel.login(email: email, password: password)
                        }
                    }
                    .buttonStyle(.borderedProminent)
                    .disabled(email.isEmpty || password.isEmpty)
                    Button(isRegistering ? "I already have an account" : "Create a parent account") {
                        isRegistering.toggle()
                    }
                    .font(.subheadline.weight(.semibold))
                }
                .padding(.horizontal, 24)

                if let error = appModel.liveStore.lastErrorMessage {
                    Text(error)
                        .font(.footnote)
                        .foregroundStyle(.red)
                        .multilineTextAlignment(.center)
                        .padding(.horizontal, 32)
                }
                Text("Sign in to securely load your family’s live learning data.")
                    .font(.footnote)
                    .foregroundStyle(.secondary)
                    .multilineTextAlignment(.center)
                    .padding(.horizontal, 32)
                Spacer()
            }
            .navigationTitle("app_name")
            .navigationBarTitleDisplayMode(.inline)
        }
    }
}
