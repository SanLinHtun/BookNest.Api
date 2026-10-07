import Foundation

struct AuthTokens: Decodable, Sendable {
    let tokenType: String
    let accessToken: String
    let expiresIn: Int
    let refreshToken: String
}
struct BookSummary: Decodable, Sendable {
    let bookId: Int64
    let title: String
    let coverImageUrl: String?
    let pageCount: Int
    let purchasePrice: Decimal
    let rentalPrice: Decimal
    let currencyCode: String
}
struct BookPage: Decodable, Sendable {
    let page: Int
    let pageSize: Int
    let total: Int
    let items: [BookSummary]
}
enum BookNestError: Error {
    case invalidResponse
    case http(Int, String)
}

actor BookNestClient {
    private let baseURL: URL
    private var accessToken: String?
    private let session: URLSession

    init(baseURL: URL, session: URLSession = .shared) {
        self.baseURL = baseURL
        self.session = session
    }
    func setAccessToken(_ value: String?) { accessToken = value }

    func login(email: String, password: String) async throws -> AuthTokens {
        let tokens: AuthTokens = try await request("api/v1/auth/login", method: "POST",
            body: ["email": email, "password": password], authenticated: false)
        accessToken = tokens.accessToken
        return tokens // Store refreshToken in Keychain, never UserDefaults.
    }
    func refresh(_ refreshToken: String) async throws -> AuthTokens {
        let tokens: AuthTokens = try await request("api/v1/auth/refresh", method: "POST",
            body: ["refreshToken": refreshToken], authenticated: false)
        accessToken = tokens.accessToken
        return tokens
    }
    func books(page: Int = 1) async throws -> BookPage {
        try await request("api/v1/books?page=\(page)&pageSize=20", authenticated: false)
    }
    func request<T: Decodable & Sendable>(_ path: String, method: String = "GET",
        body: [String: String]? = nil, authenticated: Bool = true) async throws -> T {
        guard let url = URL(string: path, relativeTo: baseURL)?.absoluteURL else {
            throw BookNestError.invalidResponse
        }
        var request = URLRequest(url: url)
        request.httpMethod = method
        request.setValue("application/json", forHTTPHeaderField: "Accept")
        if authenticated, let token = accessToken {
            request.setValue("Bearer \(token)", forHTTPHeaderField: "Authorization")
        }
        if let body {
            request.httpBody = try JSONEncoder().encode(body)
            request.setValue("application/json", forHTTPHeaderField: "Content-Type")
        }
        let (data, response) = try await session.data(for: request)
        guard let http = response as? HTTPURLResponse else { throw BookNestError.invalidResponse }
        guard (200..<300).contains(http.statusCode) else {
            throw BookNestError.http(http.statusCode, String(data: data, encoding: .utf8) ?? "")
        }
        return try JSONDecoder().decode(T.self, from: data)
    }
}
