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

    fun request(path: String, method: String = "GET", body: JSONObject? = null,
                authenticated: Boolean = true): Any? {
        val connection = URL(baseUrl.trimEnd('/') + "/" + path.trimStart('/'))
            .openConnection() as HttpURLConnection
        try {
            connection.requestMethod = method
            connection.connectTimeout = 15_000
            connection.readTimeout = 30_000
            connection.setRequestProperty("Accept", "application/json")
            if (authenticated) accessToken?.let {
                connection.setRequestProperty("Authorization", "Bearer $it")
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
