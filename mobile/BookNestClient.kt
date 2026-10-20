package com.booknest.api

import java.net.HttpURLConnection
import java.net.URL
import org.json.JSONObject
import org.json.JSONTokener

data class AuthTokens(val accessToken: String, val refreshToken: String, val expiresIn: Int)
class BookNestHttpException(val statusCode: Int, val responseBody: String) : Exception("HTTP $statusCode")

/** Android sample. Invoke blocking operations on a worker thread or Dispatchers.IO. */
class BookNestClient(private val baseUrl: String) {
    @Volatile var accessToken: String? = null

    fun register(displayName: String, email: String, password: String, confirmPassword: String): AuthTokens {
        require(displayName.isNotBlank()) { "Full name is required" }
        require(password.length in 12..128) { "Password must contain 12–128 characters" }
        require(password == confirmPassword) { "Passwords do not match" }
        val json = request("api/v1/auth/register", "POST", JSONObject()
            .put("displayName", displayName.trim()).put("email", email.trim()).put("password", password),
            authenticated = false) as JSONObject
        return tokens(json).also { accessToken = it.accessToken }
    }

    fun login(email: String, password: String): AuthTokens {
        val json = request("api/v1/auth/login", "POST",
            JSONObject().put("email", email).put("password", password), authenticated = false) as JSONObject
        return tokens(json).also { accessToken = it.accessToken }
    }
    fun refresh(refreshToken: String): AuthTokens {
        val json = request("api/v1/auth/refresh", "POST",
            JSONObject().put("refreshToken", refreshToken), authenticated = false) as JSONObject
        return tokens(json).also { accessToken = it.accessToken }
    }
    fun books(page: Int = 1): JSONObject =
        request("api/v1/books?page=$page&pageSize=20", authenticated = false) as JSONObject

    fun profile(): JSONObject = request("api/v1/me") as JSONObject
    fun home(): JSONObject = request("api/v1/home", authenticated = false) as JSONObject
    fun library(tab: String = "all", page: Int = 1): JSONObject {
        require(tab in setOf("all", "reading", "saved", "finished", "notes"))
        return request("api/v1/screens/library?tab=$tab&page=$page&pageSize=20") as JSONObject
    }
    fun collections(): JSONObject = request("api/v1/screens/collections?tab=all") as JSONObject
    fun libraryCounts(): JSONObject = request("api/v1/me/library/counts") as JSONObject
    fun readingStatistics(period: String = "week"): JSONObject {
        require(period in setOf("week", "month", "year", "all"))
        return request("api/v1/me/reading/statistics?period=$period") as JSONObject
    }
    /** Local sign-out only; the server has no token-revocation/logout endpoint. */
    fun clearSession() { accessToken = null }

    fun request(path: String, method: String = "GET", body: JSONObject? = null,
                authenticated: Boolean = true): Any? {
        val connection = URL(baseUrl.trimEnd('/') + "/" + path.trimStart('/'))
            .openConnection() as HttpURLConnection
        try {
            connection.requestMethod = method
            connection.instanceFollowRedirects = false
            connection.connectTimeout = 90_000
            connection.readTimeout = 90_000
            connection.setRequestProperty("Accept", "application/json")
            if (authenticated) {
                val token = checkNotNull(accessToken) { "Log in before requesting personal data" }
                connection.setRequestProperty("Authorization", "Bearer $token")
            }
            body?.let {
                connection.doOutput = true
                connection.setRequestProperty("Content-Type", "application/json; charset=utf-8")
                connection.outputStream.use { stream -> stream.write(it.toString().toByteArray(Charsets.UTF_8)) }
            }
            val status = connection.responseCode
            val stream = if (status in 200..299) connection.inputStream else connection.errorStream
            val text = stream?.bufferedReader(Charsets.UTF_8)?.use { it.readText() }.orEmpty()
            if (status !in 200..299) throw BookNestHttpException(status, text)
            return if (text.isBlank()) null else JSONTokener(text).nextValue()
        } finally { connection.disconnect() }
    }
    private fun tokens(json: JSONObject) = AuthTokens(
        json.getString("accessToken"), json.getString("refreshToken"), json.getInt("expiresIn"))
}
