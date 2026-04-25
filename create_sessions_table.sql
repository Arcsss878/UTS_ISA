-- Jalankan script ini di phpMyAdmin jika tabel sessions belum ada
-- (Server juga akan auto-create tabel ini saat pertama kali ada user login)

USE secure_chat;

CREATE TABLE IF NOT EXISTS sessions (
    id         INT AUTO_INCREMENT PRIMARY KEY,
    username   VARCHAR(50)  NOT NULL,
    token      VARCHAR(64)  NOT NULL UNIQUE,
    issued_at  DATETIME     NOT NULL,
    expires_at DATETIME     NOT NULL,
    is_valid   TINYINT(1)   NOT NULL DEFAULT 1
);

-- Opsional: index untuk query cepat
CREATE INDEX IF NOT EXISTS idx_sessions_username ON sessions(username);
CREATE INDEX IF NOT EXISTS idx_sessions_token    ON sessions(token);
