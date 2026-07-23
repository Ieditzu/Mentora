import Foundation
import Security

enum MentoraCredentialStore {
    private static let service = Bundle.main.bundleIdentifier ?? "io.github.kawase.mentora.ios"
    private static let sessionAccount = "parent-session"
    private static let deviceAccount = "device-id"
    private static let legacyCredentialAccount = "saved-login"

    static func deviceID() throws -> String {
        try clearLegacyCredentials()
        if let data = try loadData(account: deviceAccount),
           let deviceID = String(data: data, encoding: .utf8),
           !deviceID.isEmpty {
            return deviceID
        }

        let deviceID = UUID().uuidString
        try saveData(Data(deviceID.utf8), account: deviceAccount)
        return deviceID
    }

    static func saveSessionToken(_ sessionToken: String) throws {
        guard !sessionToken.isEmpty else {
            throw CredentialStoreError.emptySessionToken
        }
        try saveData(Data(sessionToken.utf8), account: sessionAccount)
    }

    static func loadSessionToken() throws -> String? {
        guard let data = try loadData(account: sessionAccount) else { return nil }
        guard let sessionToken = String(data: data, encoding: .utf8), !sessionToken.isEmpty else {
            throw CredentialStoreError.invalidStoredValue
        }
        return sessionToken
    }

    static func clearSessionToken() throws {
        let status = SecItemDelete(itemQuery(account: sessionAccount) as CFDictionary)
        guard status == errSecSuccess || status == errSecItemNotFound else {
            throw CredentialStoreError.keychainStatus(status)
        }
    }

    private static func clearLegacyCredentials() throws {
        let status = SecItemDelete(itemQuery(account: legacyCredentialAccount) as CFDictionary)
        guard status == errSecSuccess || status == errSecItemNotFound else {
            throw CredentialStoreError.keychainStatus(status)
        }
    }

    private static func saveData(_ data: Data, account: String) throws {
        let query = itemQuery(account: account)
        let attributes: [CFString: Any] = [
            kSecValueData: data,
            kSecAttrAccessible: kSecAttrAccessibleAfterFirstUnlockThisDeviceOnly,
        ]
        let updateStatus = SecItemUpdate(query as CFDictionary, attributes as CFDictionary)
        if updateStatus == errSecSuccess { return }
        guard updateStatus == errSecItemNotFound else {
            throw CredentialStoreError.keychainStatus(updateStatus)
        }

        var addQuery = query
        attributes.forEach { addQuery[$0.key] = $0.value }
        let addStatus = SecItemAdd(addQuery as CFDictionary, nil)
        guard addStatus == errSecSuccess else {
            throw CredentialStoreError.keychainStatus(addStatus)
        }
    }

    private static func loadData(account: String) throws -> Data? {
        var query = itemQuery(account: account)
        query[kSecReturnData] = true
        query[kSecMatchLimit] = kSecMatchLimitOne

        var result: CFTypeRef?
        let status = SecItemCopyMatching(query as CFDictionary, &result)
        switch status {
        case errSecSuccess:
            guard let data = result as? Data else {
                throw CredentialStoreError.invalidStoredValue
            }
            return data
        case errSecItemNotFound:
            return nil
        default:
            throw CredentialStoreError.keychainStatus(status)
        }
    }

    private static func itemQuery(account: String) -> [CFString: Any] {
        [
            kSecClass: kSecClassGenericPassword,
            kSecAttrService: service,
            kSecAttrAccount: account,
            kSecAttrSynchronizable: kCFBooleanFalse as Any,
        ]
    }
}

enum CredentialStoreError: LocalizedError {
    case emptySessionToken
    case invalidStoredValue
    case keychainStatus(OSStatus)

    var errorDescription: String? {
        switch self {
        case .emptySessionToken:
            return "The parent session token is empty."
        case .invalidStoredValue:
            return "Saved session information is invalid."
        case .keychainStatus(let status):
            let message = SecCopyErrorMessageString(status, nil) as String? ?? "Unknown Keychain error"
            return "Keychain error (\(status)): \(message)"
        }
    }
}
