import PhotosUI
import SwiftUI
import UIKit

/// Produces the compact, raw JPEG Base64 payload used by the Mentora profile-picture packet.
///
/// This deliberately matches Android's transport-safe profile image format: the image is
/// orientation-normalized, limited to 200 pixels on its longest side, JPEG-compressed at 70%,
/// then encoded without a data-URL prefix.
enum MentoraProfilePictureEncoder {
    static let maximumPixelDimension: CGFloat = 200
    static let jpegCompressionQuality: CGFloat = 0.70

    static func jpegBase64(from imageData: Data) throws -> String {
        guard let image = UIImage(data: imageData) else {
            throw ProfilePictureEncodingError.unsupportedImage
        }
        return try jpegBase64(from: image)
    }

    static func jpegBase64(from image: UIImage) throws -> String {
        let sourceSize = image.size
        guard sourceSize.width > 0, sourceSize.height > 0 else {
            throw ProfilePictureEncodingError.invalidDimensions
        }

        let scale = min(1, maximumPixelDimension / max(sourceSize.width, sourceSize.height))
        let targetSize = CGSize(
            width: max(1, (sourceSize.width * scale).rounded(.down)),
            height: max(1, (sourceSize.height * scale).rounded(.down))
        )

        // Rendering through UIKit applies the image orientation before serializing JPEG data.
        let format = UIGraphicsImageRendererFormat()
        format.scale = 1
        format.opaque = true
        let normalizedImage = UIGraphicsImageRenderer(size: targetSize, format: format).image { context in
            context.cgContext.setFillColor(UIColor.white.cgColor)
            context.cgContext.fill(CGRect(origin: .zero, size: targetSize))
            image.draw(in: CGRect(origin: .zero, size: targetSize))
        }

        guard let jpegData = normalizedImage.jpegData(compressionQuality: jpegCompressionQuality) else {
            throw ProfilePictureEncodingError.jpegEncodingFailed
        }
        return jpegData.base64EncodedString()
    }
}

enum ProfilePictureEncodingError: LocalizedError {
    case unsupportedImage
    case invalidDimensions
    case jpegEncodingFailed

    var errorDescription: String? {
        switch self {
        case .unsupportedImage:
            return "That photo could not be read. Please choose a different image."
        case .invalidDimensions:
            return "That photo has invalid dimensions. Please choose a different image."
        case .jpegEncodingFailed:
            return "Mentora could not prepare that photo. Please try again."
        }
    }
}

/// A reusable PhotosUI control that prepares a selected profile photo and returns its packet-ready
/// Base64 JPEG payload. The system photo picker needs no broad photo-library permission.
struct MentoraProfilePicturePicker<Label: View>: View {
    let onPictureReady: (String) -> Void
    private let label: () -> Label

    @State private var selectedItem: PhotosPickerItem?
    @State private var isProcessing = false
    @State private var errorMessage: String?

    init(
        onPictureReady: @escaping (String) -> Void,
        @ViewBuilder label: @escaping () -> Label
    ) {
        self.onPictureReady = onPictureReady
        self.label = label
    }

    var body: some View {
        PhotosPicker(selection: $selectedItem, matching: .images, photoLibrary: .shared()) {
            ZStack {
                label()
                if isProcessing {
                    Color.black.opacity(0.30)
                    ProgressView()
                        .tint(.white)
                }
            }
        }
        .disabled(isProcessing)
        .onChange(of: selectedItem) { item in
            guard let item else { return }
            Task { await process(item) }
        }
        .alert(
            "Couldn't use this photo",
            isPresented: Binding(
                get: { errorMessage != nil },
                set: { if !$0 { errorMessage = nil } }
            )
        ) {
            Button("OK", role: .cancel) {}
        } message: {
            Text(errorMessage ?? "")
        }
    }

    @MainActor
    private func process(_ item: PhotosPickerItem) async {
        isProcessing = true
        errorMessage = nil
        defer {
            isProcessing = false
            // Resetting allows the same image to be selected again later.
            selectedItem = nil
        }

        do {
            guard let imageData = try await item.loadTransferable(type: Data.self) else {
                throw ProfilePictureEncodingError.unsupportedImage
            }
            onPictureReady(try MentoraProfilePictureEncoder.jpegBase64(from: imageData))
        } catch {
            errorMessage = (error as? LocalizedError)?.errorDescription
                ?? "Mentora could not prepare that photo. Please try again."
        }
    }
}
