import SwiftUI
import UIKit

enum MentoraTheme {
    static var accent: Color {
        MentoraAccentPalette.color(for: UserDefaults.standard.string(forKey: MentoraAccentPalette.storageKey) ?? MentoraAccentPalette.defaultAccent.rawValue)
    }
    static let secondary = Color(red: 0.55, green: 0.36, blue: 0.96)
    static let success = Color(red: 0.06, green: 0.72, blue: 0.51)
    static let warning = Color(red: 0.96, green: 0.61, blue: 0.04)
    static let danger = Color(red: 0.94, green: 0.27, blue: 0.27)
    static let radius: CGFloat = 24
}

/// The same eight accent choices exposed by the Android settings screen.  The raw
/// value is stored in `UserDefaults`, so a person's chosen colour survives an app
/// restart without making a server round-trip part of a visual preference.
enum MentoraAccentPalette: String, CaseIterable, Identifiable {
    case indigo
    case violet
    case pink
    case red
    case amber
    case lime
    case emerald
    case cyan

    static let storageKey = "mentora.primaryColor"
    static let defaultAccent = MentoraAccentPalette.indigo

    var id: String { rawValue }

    var color: Color {
        switch self {
        case .indigo: return Color(red: 0.388, green: 0.400, blue: 0.945) // #6366F1
        case .violet: return Color(red: 0.545, green: 0.361, blue: 0.965) // #8B5CF6
        case .pink: return Color(red: 0.925, green: 0.282, blue: 0.600) // #EC4899
        case .red: return Color(red: 0.937, green: 0.267, blue: 0.267) // #EF4444
        case .amber: return Color(red: 0.961, green: 0.620, blue: 0.043) // #F59E0B
        case .lime: return Color(red: 0.518, green: 0.800, blue: 0.086) // #84CC16
        case .emerald: return Color(red: 0.063, green: 0.725, blue: 0.506) // #10B981
        case .cyan: return Color(red: 0.024, green: 0.714, blue: 0.831) // #06B6D4
        }
    }

    static func color(for storedValue: String) -> Color {
        MentoraAccentPalette(rawValue: storedValue)?.color ?? defaultAccent.color
    }
}

/// Shared canvas for all feature views. It deliberately stays behind scroll content,
/// so the soft colour fields never interfere with controls or accessibility contrast.
struct GlassBackground<Content: View>: View {
    let accent: Color
    @ViewBuilder var content: Content

    init(accent: Color = MentoraTheme.accent, @ViewBuilder content: () -> Content) {
        self.accent = accent
        self.content = content()
    }

    var body: some View {
        ZStack {
            Color(uiColor: .systemBackground).ignoresSafeArea()
            Circle()
                .fill(accent.opacity(0.18))
                .frame(width: 310, height: 310)
                .blur(radius: 72)
                .offset(x: -125, y: -300)
            Circle()
                .fill(accent.opacity(0.12))
                .frame(width: 360, height: 360)
                .blur(radius: 88)
                .offset(x: 150, y: 350)
            content
        }
    }
}

struct GlassCard<Content: View>: View {
    var padding: CGFloat = 18
    var cornerRadius: CGFloat = MentoraTheme.radius
    @ViewBuilder var content: Content

    init(
        padding: CGFloat = 18,
        cornerRadius: CGFloat = MentoraTheme.radius,
        @ViewBuilder content: () -> Content
    ) {
        self.padding = padding
        self.cornerRadius = cornerRadius
        self.content = content()
    }

    var body: some View {
        content
            .padding(padding)
            .background(.thinMaterial, in: RoundedRectangle(cornerRadius: cornerRadius, style: .continuous))
            .overlay {
                RoundedRectangle(cornerRadius: cornerRadius, style: .continuous)
                    .strokeBorder(.primary.opacity(0.10), lineWidth: 1)
            }
            .shadow(color: .black.opacity(0.08), radius: 18, y: 8)
    }
}

struct AvatarView: View {
    let name: String
    var imageName: String? = nil
    /// Server profile pictures are JPEG bytes encoded as Base64, matching Android.
    /// A data-URL prefix is tolerated too, which makes the renderer resilient to
    /// manually migrated account data.
    var base64Picture: String? = nil
    var accent: Color = MentoraTheme.accent
    var size: CGFloat = 56

    private var initials: String {
        name.split(separator: " ")
            .prefix(2)
            .compactMap { $0.first }
            .map(String.init)
            .joined()
            .uppercased()
    }

    private var profileImage: UIImage? {
        guard let base64Picture,
              !base64Picture.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty else {
            return nil
        }
        let encoded = base64Picture
            .trimmingCharacters(in: .whitespacesAndNewlines)
            .components(separatedBy: ",")
            .last ?? base64Picture
        guard let data = Data(base64Encoded: encoded, options: .ignoreUnknownCharacters) else {
            return nil
        }
        return UIImage(data: data)
    }

    var body: some View {
        Group {
            if let profileImage {
                Image(uiImage: profileImage)
                    .resizable()
                    .scaledToFill()
            } else if let imageName {
                Image(imageName)
                    .resizable()
                    .scaledToFill()
            } else {
                Text(initials)
                    .font(.system(size: size * 0.34, weight: .black, design: .rounded))
                    .foregroundStyle(accent)
                    .frame(maxWidth: .infinity, maxHeight: .infinity)
                    .background(accent.opacity(0.16))
            }
        }
        .frame(width: size, height: size)
        .clipShape(Circle())
        .overlay(Circle().strokeBorder(accent.opacity(0.7), lineWidth: 2))
        .accessibilityLabel(name)
    }
}

struct MentoraPageTitle: View {
    let title: String
    let subtitle: String

    var body: some View {
        VStack(alignment: .leading, spacing: 4) {
            Text(title)
                .font(.system(.largeTitle, design: .rounded, weight: .black))
            Text(subtitle)
                .font(.subheadline)
                .foregroundStyle(.secondary)
        }
    }
}

struct MentoraMetric: View {
    let label: String
    let value: String
    var tint: Color = MentoraTheme.accent

    var body: some View {
        VStack(spacing: 3) {
            Text(value)
                .font(.system(.headline, design: .rounded, weight: .black))
                .foregroundStyle(tint)
                .lineLimit(1)
                .minimumScaleFactor(0.75)
            Text(label)
                .font(.caption2.weight(.semibold))
                .foregroundStyle(.secondary)
                .lineLimit(1)
        }
        .frame(maxWidth: .infinity)
        .padding(.vertical, 10)
        .background(tint.opacity(0.10), in: RoundedRectangle(cornerRadius: 12, style: .continuous))
    }
}
