import SwiftUI
import MentoraShared
import CoreImage.CIFilterBuiltins

struct SettingsView: View {
    @EnvironmentObject private var appModel: AppModel
    @ObservedObject var store: MentoraLiveStore
    var onSignOut: (() -> Void)?
    @State private var childName = ""
    @State private var developerChildID = ""
    @State private var developerToken = ""
    @State private var showLanguagePicker = false
    @State private var securityPassword = ""
    @State private var securityCode = ""
    @AppStorage("mentora.darkMode") private var isDarkMode = false
    @AppStorage(MentoraAccentPalette.storageKey) private var accentIdentifier = MentoraAccentPalette.defaultAccent.rawValue

    private var accent: Color {
        MentoraAccentPalette.color(for: accentIdentifier)
    }

    var body: some View {
        GlassBackground(accent: accent) {
            ScrollView {
                LazyVStack(alignment: .leading, spacing: 22) {
                    MentoraPageTitle(title: "Settings", subtitle: "Personalize Mentora for your family")
                    accountCard
                    securitySection
                    languageSection
                    appearanceSection
                    childrenSection
                    developerSection
                    Button(role: .destructive) { onSignOut?() } label: {
                        if store.isSigningOut {
                            ProgressView()
                        } else {
                            Label("Sign out", systemImage: "rectangle.portrait.and.arrow.right")
                        }
                    }
                    .font(.headline.weight(.bold))
                    .frame(maxWidth: .infinity)
                    .padding(.vertical, 14)
                    .buttonStyle(.bordered)
                    .tint(MentoraTheme.danger)
                    .disabled(store.isSigningOut)
                }
                .padding(16)
                .padding(.bottom, 108)
            }
        }
        .navigationTitle("MENTORA")
        .navigationBarTitleDisplayMode(.inline)
        .sheet(isPresented: $showLanguagePicker) { languagePicker }
        .onAppear { store.fetchParentSecurityStatus() }
    }

    private var accountCard: some View {
        GlassCard(padding: 20) {
            HStack(spacing: 16) {
                MentoraProfilePicturePicker(onPictureReady: { picture in
                    store.updateProfilePicture(childID: -1, base64Picture: picture)
                }) {
                    AvatarView(
                        name: accountName,
                        base64Picture: store.snapshot.parentProfilePicture,
                        accent: accent,
                        size: 56
                    )
                }
                VStack(alignment: .leading, spacing: 3) {
                    Text("Parent account").font(.caption.weight(.bold)).foregroundStyle(accent)
                    Text(accountName).font(.headline.weight(.bold)).foregroundStyle(.primary)
                }
                Spacer()
            }
        }
    }

    private var languageSection: some View {
        settingsSection(title: "Language") {
            Button { showLanguagePicker = true } label: {
                HStack {
                    VStack(alignment: .leading, spacing: 4) {
                        Text("App language").font(.headline.weight(.bold)).foregroundStyle(.primary)
                        Text(currentLanguageName)
                            .font(.subheadline)
                            .foregroundStyle(.secondary)
                    }
                    Spacer()
                    Image(systemName: "chevron.right").font(.subheadline.weight(.bold)).foregroundStyle(.tertiary)
                }
            }
            .buttonStyle(.plain)
            Text("By default, Mentora uses the language set on this device.")
                .font(.caption).foregroundStyle(.secondary)
        }
    }

    private var securitySection: some View {
        settingsSection(title: "Security") {
            HStack {
                VStack(alignment: .leading, spacing: 4) {
                    Text("Two-factor authentication").font(.headline.weight(.bold))
                    Text("Require an authenticator or recovery code after your password.")
                        .font(.caption)
                        .foregroundStyle(.secondary)
                }
                Spacer()
                Text(store.snapshot.twoFactorEnabled ? "Enabled" : "Disabled")
                    .font(.caption.weight(.bold))
                    .foregroundStyle(store.snapshot.twoFactorEnabled ? MentoraTheme.success : .secondary)
            }

            if let details = store.totpEnrollmentDetails {
                Divider().overlay(.primary.opacity(0.10))
                Text("Scan this QR code with your authenticator app.")
                    .font(.subheadline)
                    .foregroundStyle(.secondary)
                totpQRCode(details.otpAuthURI)
                    .frame(width: 210, height: 210)
                    .frame(maxWidth: .infinity)
                Text("Manual setup key").font(.caption.weight(.bold))
                Text(details.secretBase32)
                    .font(.system(.body, design: .monospaced))
                    .textSelection(.enabled)
                TextField("Six-digit code", text: $securityCode)
                    .keyboardType(.numberPad)
                    .textContentType(.oneTimeCode)
                    .textFieldStyle(.roundedBorder)
                Button("Confirm two-factor authentication") {
                    store.confirmTotpEnrollment(code: securityCode)
                    securityCode = ""
                }
                .buttonStyle(.borderedProminent)
                .tint(accent)
                .disabled(
                    !store.isConnected ||
                    store.isSecurityRequestPending ||
                    securityCode.count != 6
                )
            } else {
                SecureField("Current password", text: $securityPassword)
                    .textContentType(.password)
                    .textFieldStyle(.roundedBorder)
                if store.snapshot.twoFactorEnabled {
                    TextField("Authenticator or recovery code", text: $securityCode)
                        .textContentType(.oneTimeCode)
                        .textFieldStyle(.roundedBorder)
                    Button(role: .destructive) {
                        store.disableTotp(password: securityPassword, code: securityCode)
                        securityPassword = ""
                        securityCode = ""
                    } label: {
                        Text("Disable two-factor authentication")
                            .frame(maxWidth: .infinity)
                    }
                    .buttonStyle(.borderedProminent)
                    .disabled(
                        !store.isConnected ||
                        store.isSecurityRequestPending ||
                        securityPassword.isEmpty ||
                        securityCode.isEmpty
                    )
                } else {
                    Button {
                        store.beginTotpEnrollment(password: securityPassword)
                        securityPassword = ""
                    } label: {
                        Text("Enable two-factor authentication")
                            .frame(maxWidth: .infinity)
                    }
                    .buttonStyle(.borderedProminent)
                    .tint(accent)
                    .disabled(
                        !store.isConnected ||
                        store.isSecurityRequestPending ||
                        securityPassword.isEmpty
                    )
                }
            }

            if store.isSecurityRequestPending {
                ProgressView()
                    .frame(maxWidth: .infinity)
            }

            if !store.recoveryCodes.isEmpty {
                Divider().overlay(.primary.opacity(0.10))
                Text("Save these one-time recovery codes. They will not be shown again.")
                    .font(.subheadline.weight(.bold))
                VStack(alignment: .leading, spacing: 6) {
                    ForEach(store.recoveryCodes, id: \.self) { recoveryCode in
                        Text(recoveryCode).font(.system(.body, design: .monospaced))
                    }
                }
                .textSelection(.enabled)
                Button("I saved these codes") {
                    store.clearTotpEnrollmentResult()
                }
                .buttonStyle(.bordered)
            } else if store.snapshot.twoFactorEnabled {
                Text("\(store.snapshot.recoveryCodesRemaining) recovery codes remaining")
                    .font(.caption)
                    .foregroundStyle(.secondary)
            }
        }
    }

    @ViewBuilder
    private func totpQRCode(_ contents: String) -> some View {
        let filter = CIFilter.qrCodeGenerator()
        filter.message = Data(contents.utf8)
        if let outputImage = filter.outputImage?.transformed(by: CGAffineTransform(scaleX: 8, y: 8)),
           let cgImage = CIContext().createCGImage(outputImage, from: outputImage.extent) {
            Image(decorative: cgImage, scale: 1)
                .interpolation(.none)
                .resizable()
                .background(.white)
        } else {
            Image(systemName: "qrcode")
                .resizable()
                .scaledToFit()
        }
    }

    private var appearanceSection: some View {
        settingsSection(title: "App theme") {
            Toggle(isOn: $isDarkMode) {
                Label("Dark mode", systemImage: "moon.fill")
                    .font(.headline.weight(.bold))
            }
            .tint(accent)
            Divider().overlay(.primary.opacity(0.10))
            Text("Theme color")
                .font(.subheadline.weight(.bold))
            LazyVGrid(
                columns: Array(repeating: GridItem(.flexible(), spacing: 12), count: 4),
                spacing: 12
            ) {
                ForEach(MentoraAccentPalette.allCases) { option in
                    Button {
                        accentIdentifier = option.rawValue
                    } label: {
                        Circle()
                            .fill(option.color)
                            .frame(width: 44, height: 44)
                            .overlay {
                                Circle()
                                    .strokeBorder(
                                        accentIdentifier == option.rawValue ? Color.primary : Color.clear,
                                        lineWidth: 3
                                    )
                            }
                            .overlay {
                                if accentIdentifier == option.rawValue {
                                    Image(systemName: "checkmark")
                                        .font(.caption.weight(.black))
                                        .foregroundStyle(.white)
                                }
                            }
                    }
                    .buttonStyle(.plain)
                    .accessibilityLabel("Use \(option.rawValue) theme color")
                    .accessibilityValue(accentIdentifier == option.rawValue ? "Selected" : "")
                }
            }
            Text("Choose whether Mentora uses its dark appearance.")
                .font(.subheadline)
                .foregroundStyle(.secondary)
        }
    }

    private var childrenSection: some View {
        VStack(alignment: .leading, spacing: 12) {
            Text("Manage kids").font(.title3.weight(.heavy))
            ForEach(store.snapshot.children, id: \.id) { child in
                GlassCard(padding: 14, cornerRadius: 20) {
                    HStack(spacing: 12) {
                        MentoraProfilePicturePicker(onPictureReady: { picture in
                            store.updateProfilePicture(childID: child.id, base64Picture: picture)
                        }) {
                            AvatarView(
                                name: child.name,
                                base64Picture: child.profilePicture,
                                accent: accent,
                                size: 46
                            )
                        }
                        Text(child.name).font(.headline.weight(.bold)).foregroundStyle(.primary)
                        Spacer()
                        Button(role: .destructive) { store.removeChild(id: child.id) } label: { Image(systemName: "trash") }
                            .tint(MentoraTheme.danger)
                            .accessibilityLabel("Remove \(child.name)")
                    }
                }
            }
            GlassCard {
                VStack(spacing: 14) {
                    TextField("Child's name", text: $childName)
                        .textInputAutocapitalization(.words)
                        .textFieldStyle(.roundedBorder)
                    Button {
                        store.addChild(named: childName)
                        childName = ""
                    } label: {
                        Label("Add child", systemImage: "person.badge.plus")
                            .frame(maxWidth: .infinity)
                    }
                    .buttonStyle(.borderedProminent)
                    .tint(accent)
                    .disabled(childName.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty)
                }
            }
        }
    }

    private var developerSection: some View {
        settingsSection(title: "Developer options", titleColor: MentoraTheme.danger) {
            TextField("Manual child ID", text: $developerChildID)
                .keyboardType(.numberPad).textFieldStyle(.roundedBorder)
            TextField("Manual token", text: $developerToken)
                .textInputAutocapitalization(.never).textFieldStyle(.roundedBorder)
            Button("Force game login") {
                guard let childID = Int64(developerChildID) else { return }
                store.claimQRLogin(token: developerToken, for: childID)
                developerToken = ""
            }
                .buttonStyle(.borderedProminent).tint(MentoraTheme.danger)
                .disabled(developerChildID.isEmpty || developerToken.isEmpty)
        }
    }

    private func settingsSection<Content: View>(title: String, titleColor: Color = .primary, @ViewBuilder content: () -> Content) -> some View {
        VStack(alignment: .leading, spacing: 12) {
            Text(title).font(.title3.weight(.heavy)).foregroundStyle(titleColor)
            GlassCard {
                VStack(alignment: .leading, spacing: 14) { content() }
            }
        }
    }

    private var accountName: String {
        if !appModel.email.isEmpty { return appModel.email }
        guard store.snapshot.parentId >= 0 else { return "Parent account" }
        return "Parent #\(store.snapshot.parentId)"
    }

    private var currentLanguageName: String {
        appModel.languageOptions
            .first { $0.tag == appModel.selectedLanguagePreference }?
            .nativeName ?? "Use device language"
    }

    private var languagePicker: some View {
        NavigationStack {
            List {
                Button {
                    appModel.applyLanguage("system")
                    store.setLanguage(appModel.resolvedLanguageTag)
                    showLanguagePicker = false
                } label: {
                    HStack {
                        Text("Use device language").foregroundStyle(.primary)
                        Spacer()
                        if appModel.selectedLanguagePreference == "system" {
                            Image(systemName: "checkmark").foregroundStyle(accent)
                        }
                    }
                }
                ForEach(appModel.languageOptions, id: \.tag) { language in
                Button {
                    appModel.applyLanguage(language.tag)
                    store.setLanguage(appModel.resolvedLanguageTag)
                    showLanguagePicker = false
                } label: {
                    HStack {
                        Text(language.nativeName).foregroundStyle(.primary)
                        Spacer()
                        if language.tag == appModel.selectedLanguagePreference {
                            Image(systemName: "checkmark").foregroundStyle(accent)
                        }
                    }
                }
                }
            }
            .navigationTitle("App language")
            .toolbar { ToolbarItem(placement: .topBarTrailing) { Button("Done") { showLanguagePicker = false } } }
        }
    }
}
