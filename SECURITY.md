# Security Policy

## Supported Versions

| Version | Supported          |
| ------- | ------------------ |
| 1.x.x   | :white_check_mark: |

## Reporting a Vulnerability

We take the security of Capitrack seriously. If you have discovered a security vulnerability in our project, please report it to us responsibly.

### How to Report

1. **Do NOT** open a public GitHub issue for security vulnerabilities
2. Send a detailed report to the repository maintainer via GitHub's private vulnerability reporting feature
3. Alternatively, create a private security advisory in the repository

### What to Include

Please include the following in your report:

- A clear description of the vulnerability
- Steps to reproduce the issue
- Potential impact of the vulnerability
- Any suggested fixes (if applicable)

### What to Expect

- **Initial Response**: We aim to acknowledge receipt of your report within 48 hours
- **Status Updates**: We will keep you informed about our progress
- **Resolution Timeline**: We strive to resolve critical vulnerabilities within 7-14 days
- **Credit**: We will credit reporters in our release notes (unless you prefer to remain anonymous)

## Security Best Practices for Users

### Database Security

- Store your SQLite database file in a secure location with appropriate file permissions
- Regularly backup your database file
- Do not share your database file publicly

### Authentication

- Use strong, unique passwords (minimum 8 characters with uppercase, lowercase, numbers, and special characters)
- Change the default session secret in production environments

### Deployment

- Always use HTTPS in production
- Keep your dependencies up to date
- Review and restrict network access to the application

### Docker Deployment

- Use environment variables for sensitive configuration
- Do not expose unnecessary ports
- Keep the base Docker image updated

## Security Features

Capitrack includes several security features:

- **Password Hashing**: Passwords are hashed using bcrypt with 12 rounds
- **Session Management**: Secure session handling with httpOnly cookies
- **Input Validation**: All user inputs are validated and sanitized
- **SQL Injection Prevention**: Parameterized queries using better-sqlite3
- **CSRF Protection**: SameSite cookie policy enabled

## Changelog

Security-related changes will be documented in our release notes with appropriate CVE references when applicable.
