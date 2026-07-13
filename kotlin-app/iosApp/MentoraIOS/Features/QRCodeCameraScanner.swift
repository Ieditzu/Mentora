import AVFoundation
import SwiftUI
import UIKit

/// A production camera-backed QR scanner that returns the first QR payload it reads.
///
/// The scanner requests camera access only when it is presented. Callers should dismiss
/// their sheet after receiving a token; the scanner also stops its capture session as soon
/// as it reports a value, which prevents duplicate game-login requests.
struct QRCodeCameraScanner: UIViewControllerRepresentable {
    let onScan: (String) -> Void
    let onFailure: (QRCodeCameraScannerFailure) -> Void

    func makeCoordinator() -> Coordinator {
        Coordinator(onScan: onScan, onFailure: onFailure)
    }

    func makeUIViewController(context: Context) -> QRCodeCameraScannerViewController {
        let controller = QRCodeCameraScannerViewController()
        controller.delegate = context.coordinator
        return controller
    }

    func updateUIViewController(_ uiViewController: QRCodeCameraScannerViewController, context: Context) {}

    static func dismantleUIViewController(
        _ uiViewController: QRCodeCameraScannerViewController,
        coordinator: Coordinator
    ) {
        uiViewController.stopScanning()
    }

    final class Coordinator: NSObject, QRCodeCameraScannerViewControllerDelegate {
        private let onScan: (String) -> Void
        private let onFailure: (QRCodeCameraScannerFailure) -> Void

        init(
            onScan: @escaping (String) -> Void,
            onFailure: @escaping (QRCodeCameraScannerFailure) -> Void
        ) {
            self.onScan = onScan
            self.onFailure = onFailure
        }

        func cameraScanner(_ scanner: QRCodeCameraScannerViewController, didScan token: String) {
            onScan(token)
        }

        func cameraScanner(
            _ scanner: QRCodeCameraScannerViewController,
            didFail failure: QRCodeCameraScannerFailure
        ) {
            onFailure(failure)
        }
    }
}

enum QRCodeCameraScannerFailure: LocalizedError, Equatable {
    case permissionDenied
    case cameraUnavailable
    case configurationFailed

    var errorDescription: String? {
        switch self {
        case .permissionDenied:
            return "Camera access is needed to scan the game QR code. Enable it in Settings, or enter the token manually."
        case .cameraUnavailable:
            return "No camera is available on this device. Enter the game token manually instead."
        case .configurationFailed:
            return "The camera scanner could not start. Enter the game token manually instead."
        }
    }
}

protocol QRCodeCameraScannerViewControllerDelegate: AnyObject {
    func cameraScanner(_ scanner: QRCodeCameraScannerViewController, didScan token: String)
    func cameraScanner(_ scanner: QRCodeCameraScannerViewController, didFail failure: QRCodeCameraScannerFailure)
}

final class QRCodeCameraScannerViewController: UIViewController {
    weak var delegate: QRCodeCameraScannerViewControllerDelegate?

    private let captureSession = AVCaptureSession()
    private let sessionQueue = DispatchQueue(label: "io.github.kawase.mentora.qr-camera")
    private let metadataQueue = DispatchQueue(label: "io.github.kawase.mentora.qr-metadata")
    private var previewLayer: AVCaptureVideoPreviewLayer?
    private var isConfigured = false
    private var hasReportedResult = false
    private let resultLock = NSLock()

    override func viewDidLoad() {
        super.viewDidLoad()
        view.backgroundColor = .black
        requestPermissionAndConfigure()
    }

    override func viewDidLayoutSubviews() {
        super.viewDidLayoutSubviews()
        previewLayer?.frame = view.bounds
    }

    override func viewWillAppear(_ animated: Bool) {
        super.viewWillAppear(animated)
        startScanning()
    }

    override func viewWillDisappear(_ animated: Bool) {
        super.viewWillDisappear(animated)
        stopScanning()
    }

    func startScanning() {
        sessionQueue.async { [weak self] in
            guard let self, self.isConfigured, !self.resultWasReported, !self.captureSession.isRunning else {
                return
            }
            self.captureSession.startRunning()
        }
    }

    func stopScanning() {
        sessionQueue.async { [weak self] in
            guard let self, self.captureSession.isRunning else { return }
            self.captureSession.stopRunning()
        }
    }

    private func requestPermissionAndConfigure() {
        switch AVCaptureDevice.authorizationStatus(for: .video) {
        case .authorized:
            configureSession()
        case .notDetermined:
            AVCaptureDevice.requestAccess(for: .video) { [weak self] granted in
                DispatchQueue.main.async {
                    guard let self else { return }
                    granted ? self.configureSession() : self.reportFailure(.permissionDenied)
                }
            }
        case .denied, .restricted:
            reportFailure(.permissionDenied)
        @unknown default:
            reportFailure(.permissionDenied)
        }
    }

    private func configureSession() {
        sessionQueue.async { [weak self] in
            guard let self, !self.isConfigured else { return }
            guard let camera = AVCaptureDevice.default(for: .video) else {
                self.reportFailure(.cameraUnavailable)
                return
            }

            do {
                let input = try AVCaptureDeviceInput(device: camera)
                guard self.captureSession.canAddInput(input) else {
                    self.reportFailure(.configurationFailed)
                    return
                }

                let output = AVCaptureMetadataOutput()
                guard self.captureSession.canAddOutput(output) else {
                    self.reportFailure(.configurationFailed)
                    return
                }

                self.captureSession.beginConfiguration()
                self.captureSession.addInput(input)
                self.captureSession.addOutput(output)
                output.setMetadataObjectsDelegate(self, queue: self.metadataQueue)
                output.metadataObjectTypes = [.qr]
                self.captureSession.commitConfiguration()
                self.isConfigured = true

                DispatchQueue.main.async { [weak self] in
                    guard let self else { return }
                    let previewLayer = AVCaptureVideoPreviewLayer(session: self.captureSession)
                    previewLayer.videoGravity = .resizeAspectFill
                    previewLayer.frame = self.view.bounds
                    self.view.layer.insertSublayer(previewLayer, at: 0)
                    self.previewLayer = previewLayer
                    self.startScanning()
                }
            } catch {
                self.reportFailure(.configurationFailed)
            }
        }
    }

    private func reportFailure(_ failure: QRCodeCameraScannerFailure) {
        DispatchQueue.main.async { [weak self] in
            guard let self else { return }
            self.delegate?.cameraScanner(self, didFail: failure)
        }
    }

    private var resultWasReported: Bool {
        resultLock.lock()
        defer { resultLock.unlock() }
        return hasReportedResult
    }

    private func reportResultOnce() -> Bool {
        resultLock.lock()
        defer { resultLock.unlock() }
        guard !hasReportedResult else { return false }
        hasReportedResult = true
        return true
    }
}

extension QRCodeCameraScannerViewController: AVCaptureMetadataOutputObjectsDelegate {
    func metadataOutput(
        _ output: AVCaptureMetadataOutput,
        didOutput metadataObjects: [AVMetadataObject],
        from connection: AVCaptureConnection
    ) {
        guard
            let object = metadataObjects.first as? AVMetadataMachineReadableCodeObject,
            object.type == .qr,
            let token = object.stringValue?.trimmingCharacters(in: .whitespacesAndNewlines),
            !token.isEmpty
        else {
            return
        }

        guard reportResultOnce() else { return }
        stopScanning()
        DispatchQueue.main.async { [weak self] in
            guard let self else { return }
            self.delegate?.cameraScanner(self, didScan: token)
        }
    }
}
