# Contributing to Capitrack

First off, thank you for considering contributing to Capitrack! It's people like you that make Capitrack such a great tool.

## Code of Conduct

By participating in this project, you are expected to uphold our Code of Conduct:

- Be respectful and inclusive
- Be patient with newcomers
- Focus on constructive feedback
- Accept responsibility for your mistakes

## How Can I Contribute?

### Reporting Bugs

Before creating bug reports, please check existing issues to avoid duplicates. When you create a bug report, include as many details as possible:

- **Use a clear and descriptive title**
- **Describe the exact steps to reproduce the problem**
- **Provide specific examples to demonstrate the steps**
- **Describe the behavior you observed and what you expected**
- **Include screenshots if applicable**
- **Include your environment details** (OS, Node.js version, browser)

### Suggesting Enhancements

Enhancement suggestions are tracked as GitHub issues. When creating an enhancement suggestion:

- **Use a clear and descriptive title**
- **Provide a detailed description of the proposed enhancement**
- **Explain why this enhancement would be useful**
- **List any alternatives you've considered**

### Pull Requests

1. **Fork the repository** and create your branch from `master`
2. **Follow the coding style** of the project
3. **Add tests** if you've added code that should be tested
4. **Ensure the test suite passes**
5. **Update documentation** as needed
6. **Write a clear commit message**

## Development Setup

### Prerequisites

- Node.js 18.x or higher
- npm 9.x or higher

### Installation

```bash
# Clone your fork
git clone https://github.com/your-username/Capitrack.git
cd Capitrack

# Install dependencies
npm install

# Build TypeScript
npm run build

# Run in development mode
npm run dev

# Run tests
npm test
```

### Project Structure

```
capitrack/
├── src/
│   ├── db/           # Database initialization and migrations
│   ├── middleware/   # Express middleware
│   ├── routes/       # API route handlers
│   ├── services/     # Business logic services
│   ├── types/        # TypeScript type definitions
│   ├── public/       # Frontend static files
│   │   ├── css/      # Stylesheets
│   │   └── js/       # Frontend JavaScript modules
│   └── server.ts     # Express application entry point
├── tests/            # Test files
├── data/             # Database storage (gitignored)
└── dist/             # Compiled JavaScript output
```

### Coding Guidelines

#### TypeScript

- Use TypeScript for all new backend code
- Define interfaces for all data structures
- Use strict type checking

#### Frontend JavaScript

- The frontend uses vanilla JavaScript with ES modules
- Follow existing patterns for API calls and state management

#### CSS

- Use CSS custom properties for theming
- Follow the BEM-like naming convention used in the project
- Ensure styles work in both dark and light themes

#### Testing

- Write tests for new functionality
- Ensure all existing tests pass before submitting

### Commit Messages

Follow the conventional commits specification:

```
type(scope): description

[optional body]

[optional footer]
```

Types:
- `feat`: New feature
- `fix`: Bug fix
- `docs`: Documentation only
- `style`: Code style changes (formatting, etc.)
- `refactor`: Code refactoring
- `test`: Adding or updating tests
- `chore`: Maintenance tasks

Examples:
```
feat(accounts): add support for multiple currencies
fix(transactions): resolve calculation error in holdings
docs(readme): update installation instructions
```

## Review Process

1. All submissions require review
2. We aim to review pull requests within a week
3. Feedback will be provided through GitHub's review system
4. Changes may be requested before merging

## Recognition

Contributors will be recognized in our release notes. Thank you for helping make Capitrack better!

## Questions?

Feel free to open an issue with the "question" label if you have any questions about contributing.
