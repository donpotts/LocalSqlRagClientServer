# LocalSQL RAG Client-Server 🚀

> **A Modern Natural Language to SQL Interface with AI Integration**

[![.NET](https://img.shields.io/badge/.NET-9.0-512BD4?style=flat-square&logo=dotnet)](https://dotnet.microsoft.com/)
[![Blazor](https://img.shields.io/badge/Blazor-WebAssembly-512BD4?style=flat-square&logo=blazor)](https://blazor.net/)
[![SQLite](https://img.shields.io/badge/SQLite-003B57?style=flat-square&logo=sqlite)](https://sqlite.org/)

A sophisticated client-server application that transforms natural language queries into SQL using local AI models. Built with .NET 9, Blazor WebAssembly, and integrated with Ollama for privacy-focused AI processing.

## 🌟 Features

### 🤖 AI-Powered SQL Generation
- **Natural Language Processing**: Convert English questions to SQL queries
- **Local AI Models**: Uses Ollama (Phi3) for complete privacy
- **Smart Query Optimization**: Context-aware SQL generation with few-shot learning
- **Multi-layered Security**: SQL injection protection and query validation

### 🔐 Enterprise-Grade Security
- **JWT Authentication**: Stateless, secure token-based authentication
- **Role-Based Access**: Admin/User roles with granular permissions
- **Query Restrictions**: Read-only mode for regular users
- **Input Sanitization**: Comprehensive SQL and input validation

### 💼 Professional UI/UX
- **Modern Blazor Interface**: Responsive, component-based architecture
- **Real-time Chat**: Interactive query interface with typing indicators
- **Admin Dashboard**: User management and system administration
- **Professional Design**: Bootstrap 5 with custom styling

### 📊 Data Management
- **Employee Database**: Complete CRUD operations with smart defaults
- **Database Schema**: Optimized SQLite with proper constraints
- **Data Validation**: Comprehensive validation at model and database levels
- **Query History**: Track and manage query interactions

## 🏗️ Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                    Client (Blazor WASM)                     │
├─────────────────────────────────────────────────────────────┤
│  • Authentication Service     • Admin Service              │
│  • Chat Interface            • Employee Management         │
│  • Bootstrap UI              • Local Storage               │
└─────────────────────┬───────────────────────────────────────┘
                      │ HTTPS/JWT
┌─────────────────────▼───────────────────────────────────────┐
│                 Server (ASP.NET Core)                      │
├─────────────────────────────────────────────────────────────┤
│  • JWT Authentication        • SQL Query Validation        │
│  • Text-to-SQL Service       • Database Service            │
│  • Semantic Kernel           • Admin Controller            │
└─────────────────────┬───────────────────────────────────────┘
                      │
            ┌─────────▼────────┐    ┌─────────────────────┐
            │   SQLite DBs     │    │   Ollama (Phi3)     │
            │                  │    │                     │
            │ • app.db         │    │ • Local AI Model    │
            │ • company.db     │    │ • Privacy-focused   │
            └──────────────────┘    └─────────────────────┘
```

## 🚀 Quick Start

### Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [Ollama](https://ollama.com/) with Phi3 model
- Modern web browser with WebAssembly support

### Installation

1. **Clone the repository**
   ```bash
   git clone https://github.com/yourusername/LocalSQLRAGClientServer.git
   cd LocalSQLRAGClientServer
   ```

2. **Install Ollama and Phi3 model**
   ```bash
   # Install Ollama from https://ollama.com/
   ollama pull phi3:3.8b
   ollama serve
   ```

3. **Configure environment variables** (Production)
   ```bash
   # Set a secure JWT secret (32+ characters)
   export JWT_SECRET_KEY="your-super-secure-jwt-secret-key-here-32-chars-minimum"
   ```

4. **Run the application**
   ```bash
   # Start the server (Terminal 1)
   cd LocalTextToSqlChat.Server
   dotnet run

   # Start the client (Terminal 2)
   cd LocalTextToSqlChat.Client
   dotnet run
   ```

5. **Access the application**
   - Client: `https://localhost:7115`
   - Server API: `https://localhost:7267`
   - Health Check: `https://localhost:7267/health`

## 💻 Usage

### Getting Started

1. **Create an account** - First user automatically becomes admin
2. **Start chatting** - Ask natural language questions about employees
3. **Explore examples** - Click suggested prompts to see capabilities

### Example Queries

```sql
-- Natural Language → Generated SQL
"Show me all employees"
→ SELECT * FROM Employees

"Who earns more than 80000?"
→ SELECT Name, Department, Salary FROM Employees WHERE Salary > 80000

"List employees by department"
→ SELECT Name, Department, Salary FROM Employees ORDER BY Department, Name

"Add new employee John Smith to Engineering"
→ INSERT INTO Employees (Name, Department) VALUES ('John Smith', 'Engineering')
```

### Admin Features

- **User Management**: Create, update, delete users
- **Role Assignment**: Promote users to admin
- **Employee CRUD**: Full employee management via API
- **System Monitoring**: Health checks and diagnostics

## 🔧 Configuration

### Environment Variables

| Variable | Description | Default | Required |
|----------|-------------|---------|----------|
| `JWT_SECRET_KEY` | JWT signing key (32+ chars) | - | Yes (Production) |
| `ASPNETCORE_ENVIRONMENT` | Environment name | Development | No |

### Configuration Files

**appsettings.json**
```json
{
  "Jwt": {
    "SecretKey": "",  // Leave empty for production
    "Issuer": "LocalTextToSqlChat",
    "Audience": "LocalTextToSqlChat.Client"
  },
  "OllamaSettings": {
    "BaseUrl": "http://localhost:11434",
    "ModelId": "phi3:3.8b"
  }
}
```

## 🛡️ Security Features

### Multi-Layer Protection
- **SQL Injection Prevention**: Parameterized queries + validation
- **Query Intent Analysis**: Prevents unauthorized write operations
- **JWT Security**: Environment-based secrets + proper validation
- **CORS Protection**: Environment-specific allowed origins
- **Security Headers**: XSS, CSRF, content-type protection

### Best Practices Implemented
- ✅ Secure password hashing (BCrypt)
- ✅ Token expiration and validation
- ✅ Role-based authorization
- ✅ Input sanitization and validation
- ✅ Environment-based configuration
- ✅ Health monitoring endpoints

## 🧪 Testing

### API Testing
```bash
# Health check
curl https://localhost:7267/health

# Authentication
curl -X POST https://localhost:7267/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{"username":"test","email":"test@example.com","password":"Test123!"}'
```

### Database Testing
The application includes comprehensive employee management with smart defaults:
- **HireDate**: Automatically set to current date
- **Department**: Defaults to "Unknown" if not specified
- **Salary**: Defaults to 0 if not provided

## 🏗️ Development

### Project Structure
```
LocalSQLRAGClientServer/
├── LocalTextToSqlChat.Client/     # Blazor WebAssembly
│   ├── Pages/                     # Razor pages
│   ├── Services/                  # Client services
│   └── Models/                    # Client models
├── LocalTextToSqlChat.Server/     # ASP.NET Core API
│   ├── Controllers/               # API controllers
│   ├── Services/                  # Business logic
│   ├── Data/                      # Database layer
│   └── Models/                    # Data models
└── README.md
```

### Key Technologies
- **Frontend**: Blazor WebAssembly, Bootstrap 5, SignalR-ready
- **Backend**: ASP.NET Core 9, Entity Framework alternative
- **Database**: SQLite with migration support
- **AI/ML**: Semantic Kernel, Ollama integration
- **Authentication**: JWT Bearer tokens
- **Security**: BCrypt, CORS, Security Headers

## 🚀 Deployment

### Production Checklist

- [ ] Set `JWT_SECRET_KEY` environment variable
- [ ] Configure production database connection
- [ ] Set up HTTPS certificates
- [ ] Configure CORS for production domains
- [ ] Enable application logging
- [ ] Set up monitoring and health checks
- [ ] Review and test security configurations

### Docker Support (Coming Soon)
```dockerfile
# Dockerfile example (future enhancement)
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
# ... Docker configuration
```

### Development Guidelines
- Follow C# coding standards
- Add unit tests for new features
- Update documentation
- Ensure security best practices

## 🙏 Acknowledgments

- **Ollama Team** - Local AI model hosting
- **Microsoft** - Semantic Kernel framework
- **Blazor Community** - WebAssembly innovations
- **.NET Foundation** - Outstanding development platform

---

**Built with ❤️ using .NET 9 and Blazor WebAssembly**
