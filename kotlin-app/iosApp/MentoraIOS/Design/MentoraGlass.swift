import SwiftUI

enum MentoraTheme {
    static let accent = Color(red: 0.39, green: 0.40, blue: 0.95)
    static let success = Color(red: 0.06, green: 0.72, blue: 0.51)
    static let warning = Color(red: 0.96, green: 0.61, blue: 0.04)
    static let danger = Color(red: 0.94, green: 0.27, blue: 0.27)
    static let radius: CGFloat = 24
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

    var body: some View {
        Group {
            if let imageName {
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
