import SwiftUI

struct SettingsView: View {
    @ObservedObject var store: MentoraPreviewStore
    var onSignOut: (() -> Void)?
    @State private var childName = ""
    @State private var developerChildID = ""
    @State private var developerToken = ""
    @State private var showLanguagePicker = false

    private let colors: [Color] = [
        Color(red: 0.39, green: 0.40, blue: 0.95), .purple, .pink, .red,
        MentoraTheme.warning, .green, MentoraTheme.success, .cyan
    ]

    var body: some View {
        GlassBackground(accent: store.accent) {
            ScrollView {
                LazyVStack(alignment: .leading, spacing: 22) {
                    MentoraPageTitle(title: "Settings", subtitle: "Personalize Mentora for your family")
                    accountCard
                    languageSection
                    appearanceSection
                    childrenSection
                    developerSection
                    Button(role: .destructive) { onSignOut?() } label: {
                        Label("Sign out", systemImage: "rectangle.portrait.and.arrow.right")
                            .font(.headline.weight(.bold))
                            .frame(maxWidth: .infinity)
                            .padding(.vertical, 14)
                    }
                    .buttonStyle(.bordered)
                    .tint(MentoraTheme.danger)
                }
                .padding(24)
                .padding(.bottom, 108)
            }
        }
        .navigationTitle("MENTORA")
        .navigationBarTitleDisplayMode(.inline)
        .sheet(isPresented: $showLanguagePicker) { languagePicker }
    }

    private var accountCard: some View {
        GlassCard(padding: 20) {
            HStack(spacing: 16) {
                AvatarView(name: store.parentEmail, accent: store.accent, size: 56)
                VStack(alignment: .leading, spacing: 3) {
                    Text("Parent account").font(.caption.weight(.bold)).foregroundStyle(store.accent)
                    Text(store.parentEmail).font(.headline.weight(.bold)).foregroundStyle(.primary)
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
                        Text(currentLanguage.nativeName).font(.subheadline).foregroundStyle(.secondary)
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

    private var appearanceSection: some View {
        settingsSection(title: "App theme") {
            Toggle(isOn: $store.isDarkMode) {
                Label("Dark mode", systemImage: "moon.fill")
                    .font(.headline.weight(.bold))
            }
            .tint(store.accent)
            Divider().overlay(.primary.opacity(0.10))
            Text("Theme color").font(.subheadline.weight(.bold))
            LazyVGrid(columns: Array(repeating: GridItem(.flexible(), spacing: 12), count: 4), spacing: 12) {
                ForEach(colors.indices, id: \.self) { index in
                    let color = colors[index]
                    Button { store.accent = color } label: {
                        Circle().fill(color).frame(height: 38)
                            .overlay {
                                if store.accent == color {
                                    Image(systemName: "checkmark").font(.caption.weight(.black)).foregroundStyle(.white)
                                }
                            }
                            .overlay(Circle().strokeBorder(.white.opacity(0.7), lineWidth: 2))
                    }
                    .buttonStyle(.plain)
                    .accessibilityLabel("Select theme color")
                }
            }
        }
    }

    private var childrenSection: some View {
        VStack(alignment: .leading, spacing: 12) {
            Text("Manage kids").font(.title3.weight(.heavy))
            ForEach(store.children) { child in
                GlassCard(padding: 14, cornerRadius: 20) {
                    HStack(spacing: 12) {
                        AvatarView(name: child.name, accent: store.accent, size: 46)
                        Text(child.name).font(.headline.weight(.bold)).foregroundStyle(.primary)
                        Spacer()
                        Button(role: .destructive) { store.remove(child) } label: { Image(systemName: "trash") }
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
                    .tint(store.accent)
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
            Button("Force game login") { }
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

    private var currentLanguage: MentoraLanguage {
        store.languages.first { $0.tag == store.selectedLanguage } ?? store.languages[0]
    }

    private var languagePicker: some View {
        NavigationStack {
            List(store.languages) { language in
                Button {
                    store.selectedLanguage = language.tag
                    showLanguagePicker = false
                } label: {
                    HStack {
                        Text(language.nativeName).foregroundStyle(.primary)
                        Spacer()
                        if language.tag == store.selectedLanguage {
                            Image(systemName: "checkmark").foregroundStyle(store.accent)
                        }
                    }
                }
            }
            .navigationTitle("App language")
            .toolbar { ToolbarItem(placement: .topBarTrailing) { Button("Done") { showLanguagePicker = false } } }
        }
    }
}
