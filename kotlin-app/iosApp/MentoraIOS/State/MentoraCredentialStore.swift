import Foundation
import Security

struct MentoraSavedCredentials: Equatable {
    let email: String
    let password: String
}

enum MentoraCredentialStore {
    private static let account = "saved-login"
    private static let service = Bundle.main.bundleIdentifier ?? "io.github.kawase.mentora.ios"

    static func save(email: String, password: String) throws {
        guard !email.isEmpty, !password.isEmpty else {
            throw CredentialStoreError.emptyCredentials
        }

        let credentials = try JSONEncoder().encode(
            StoredCredentials(email: email, password: password)
        )
        let query = itemQuery()
        let attributes: [CFString: Any] = [
            kSecValueData: credentials,
            kSecAttrAccessible: kSecAttrAccessibleAfterFirstUnlockThisDeviceOnly,
        ]

        let updateStatus = SecItemUpdate(query as CFDictionary, attributes as CFDictionary)
        switch updateStatus {
        case errSecSuccess:
            return
        case errSecItemNotFound:
            var addQuery = query
            attributes.forEach { addQuery[$0.key] = $0.value }
            let addStatus = SecItemAdd(addQuery as CFDictionary, nil)
            guard addStatus == errSecSuccess else {
                throw CredentialStoreError.keychainStatus(addStatus)
            }
        default:
            throw CredentialStoreError.keychainStatus(updateStatus)
        }
    }

    static func load() throws -> MentoraSavedCredentials? {
        var query = itemQuery()
        query[kSecReturnData] = true
        query[kSecMatchLimit] = kSecMatchLimitOne

        var result: CFTypeRef?
        let status = SecItemCopyMatching(query as CFDictionary, &result)
        switch status {
        case errSecSuccess:
            guard let data = result as? Data else {
                throw CredentialStoreError.invalidStoredValue
            }
            let credentials = try JSONDecoder().decode(StoredCredentials.self, from: data)
            return MentoraSavedCredentials(email: credentials.email, password: credentials.password)
        case errSecItemNotFound:
            return nil
        default:
            throw CredentialStoreError.keychainStatus(status)
        }
    }

    static func clear() throws {
        let status = SecItemDelete(itemQuery() as CFDictionary)
        guard status == errSecSuccess || status == errSecItemNotFound else {
            throw CredentialStoreError.keychainStatus(status)
        }
    }

    private static func itemQuery() -> [CFString: Any] {
        [
            kSecClass: kSecClassGenericPassword,
            kSecAttrService: service,
            kSecAttrAccount: account,
            kSecAttrSynchronizable: kCFBooleanFalse as Any,
        ]
    }
}

private struct StoredCredentials: Codable {
    let email: String
    let password: String
}

enum CredentialStoreError: LocalizedError {
    case emptyCredentials
    case invalidStoredValue
    case keychainStatus(OSStatus)

    var errorDescription: String? {
        switch self {
        case .emptyCredentials:
            return "Email and password are required."
        case .invalidStoredValue:
            return "Saved login information is invalid."
        case .keychainStatus(let status):
            let message = SecCopyErrorMessageString(status, nil) as String? ?? "Unknown Keychain error"
            return "Keychain error (\(status)): \(message)"
        }
    }
}
