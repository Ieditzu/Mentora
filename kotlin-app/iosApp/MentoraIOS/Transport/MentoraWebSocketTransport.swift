import Foundation

enum MentoraWebSocketState: Equatable {
    case disconnected
    case connecting
    case connected
    case reconnecting
}

/// A small binary WebSocket transport for the Mentora protocol.
///
/// Protocol framing and encryption deliberately stay outside this type: callers provide and
/// receive complete binary frames through `Data` so the transport can be tested independently
/// of the Kotlin shared framework.
final class MentoraWebSocketTransport: NSObject {
    typealias StateHandler = (MentoraWebSocketState) -> Void
    typealias BinaryMessageHandler = (Data) -> Void
    typealias ErrorHandler = (Error) -> Void

    var onStateChange: StateHandler?
    var onBinaryMessage: BinaryMessageHandler?
    var onError: ErrorHandler?

    private let reconnectDelay: TimeInterval = 5
    private let callbackQueue: DispatchQueue
    private let stateQueue = DispatchQueue(label: "io.github.kawase.mentora.websocket")
    private lazy var session = URLSession(
        configuration: .default,
        delegate: self,
        delegateQueue: nil
    )

    private var socket: URLSessionWebSocketTask?
    private var url: URL?
    private var reconnectWorkItem: DispatchWorkItem?
    private var shouldReconnect = false
    private var state: MentoraWebSocketState = .disconnected

    init(callbackQueue: DispatchQueue = .main) {
        self.callbackQueue = callbackQueue
        super.init()
    }

    deinit {
        reconnectWorkItem?.cancel()
        socket?.cancel(with: .goingAway, reason: nil)
        session.invalidateAndCancel()
    }

    func connect(to url: URL) {
        stateQueue.async { [weak self] in
            guard let self else { return }

            self.url = url
            self.shouldReconnect = true
            self.reconnectWorkItem?.cancel()
            self.reconnectWorkItem = nil
            self.socket?.cancel(with: .goingAway, reason: nil)
            self.socket = nil
            self.startConnection(isReconnect: false)
        }
    }

    func disconnect() {
        stateQueue.async { [weak self] in
            guard let self else { return }

            self.shouldReconnect = false
            self.reconnectWorkItem?.cancel()
            self.reconnectWorkItem = nil
            self.socket?.cancel(with: .normalClosure, reason: nil)
            self.socket = nil
            self.updateState(.disconnected)
        }
    }

    func send(_ data: Data) {
        stateQueue.async { [weak self] in
            guard let self else { return }
            guard let socket = self.socket, self.state == .connected else {
                self.reportError(MentoraWebSocketTransportError.notConnected)
                return
            }

            socket.send(.data(data)) { [weak self, weak socket] error in
                guard let self, let error else { return }
                self.stateQueue.async {
                    self.reportError(error)
                    if let socket, self.socket === socket {
                        self.socket = nil
                    }
                    self.scheduleReconnectIfNeeded()
                }
            }
        }
    }

    private func startConnection(isReconnect: Bool) {
        guard let url else {
            reportError(MentoraWebSocketTransportError.missingURL)
            updateState(.disconnected)
            return
        }

        updateState(isReconnect ? .reconnecting : .connecting)
        let socket = session.webSocketTask(with: url)
        self.socket = socket
        socket.resume()
        receiveNextMessage(from: socket)
    }

    private func receiveNextMessage(from socket: URLSessionWebSocketTask) {
        socket.receive { [weak self, weak socket] result in
            guard let self, let socket else { return }
            self.stateQueue.async {
                guard self.socket === socket else { return }

                switch result {
                case let .success(message):
                    self.updateState(.connected)
                    switch message {
                    case let .data(data):
                        self.callbackQueue.async { [weak self] in
                            self?.onBinaryMessage?(data)
                        }
                    case .string:
                        self.reportError(MentoraWebSocketTransportError.nonBinaryMessage)
                    @unknown default:
                        self.reportError(MentoraWebSocketTransportError.unsupportedMessage)
                    }
                    self.receiveNextMessage(from: socket)

                case let .failure(error):
                    self.reportError(error)
                    self.socket = nil
                    self.scheduleReconnectIfNeeded()
                }
            }
        }
    }

    private func scheduleReconnectIfNeeded() {
        guard shouldReconnect, url != nil else {
            updateState(.disconnected)
            return
        }
        guard reconnectWorkItem == nil else { return }

        updateState(.reconnecting)
        let workItem = DispatchWorkItem { [weak self] in
            guard let self else { return }
            self.stateQueue.async {
                self.reconnectWorkItem = nil
                guard self.shouldReconnect, self.socket == nil else { return }
                self.startConnection(isReconnect: true)
            }
        }
        reconnectWorkItem = workItem
        stateQueue.asyncAfter(deadline: .now() + reconnectDelay, execute: workItem)
    }

    private func updateState(_ newState: MentoraWebSocketState) {
        guard state != newState else { return }
        state = newState
        callbackQueue.async { [weak self] in
            self?.onStateChange?(newState)
        }
    }

    private func reportError(_ error: Error) {
        callbackQueue.async { [weak self] in
            self?.onError?(error)
        }
    }
}

extension MentoraWebSocketTransport: URLSessionWebSocketDelegate {
    func urlSession(
        _: URLSession,
        webSocketTask: URLSessionWebSocketTask,
        didOpenWithProtocol _: String?
    ) {
        stateQueue.async { [weak self] in
            guard let self, self.socket === webSocketTask else { return }
            self.updateState(.connected)
        }
    }

    func urlSession(
        _: URLSession,
        webSocketTask: URLSessionWebSocketTask,
        didCloseWith closeCode: URLSessionWebSocketTask.CloseCode,
        reason _: Data?
    ) {
        stateQueue.async { [weak self] in
            guard let self, self.socket === webSocketTask else { return }
            self.socket = nil
            if closeCode != .normalClosure {
                self.reportError(MentoraWebSocketTransportError.closed(closeCode))
            }
            self.scheduleReconnectIfNeeded()
        }
    }
}

private enum MentoraWebSocketTransportError: LocalizedError {
    case missingURL
    case notConnected
    case nonBinaryMessage
    case unsupportedMessage
    case closed(URLSessionWebSocketTask.CloseCode)

    var errorDescription: String? {
        switch self {
        case .missingURL:
            return "A WebSocket URL is required before connecting."
        case .notConnected:
            return "The WebSocket is not connected."
        case .nonBinaryMessage:
            return "The server sent a text WebSocket message; Mentora expects binary frames."
        case .unsupportedMessage:
            return "The server sent an unsupported WebSocket message."
        case let .closed(closeCode):
            return "The WebSocket closed with code \(closeCode.rawValue)."
        }
    }
}
