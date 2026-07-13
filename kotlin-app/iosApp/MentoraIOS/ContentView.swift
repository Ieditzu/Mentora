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
                    Button("continue") {
                        appModel.enterPreview(email: email)
                    }
                    .buttonStyle(.borderedProminent)
                    .disabled(email.isEmpty || password.isEmpty)
                }
                .padding(.horizontal, 24)

                Text("ios_socket_migration_message")
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
