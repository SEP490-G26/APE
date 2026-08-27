# 🎓 APE (AI-Powered Examination and Programming Practice Platform) — Client Web Application

<p align="center">
  <img src="https://img.shields.io/badge/React-18.3.1-61DAFB?style=for-the-badge&logo=react&logoColor=black" alt="React" />
  <img src="https://img.shields.io/badge/Vite-5.4.10-646CFF?style=for-the-badge&logo=vite&logoColor=white" alt="Vite" />
  <img src="https://img.shields.io/badge/Monaco_Editor-4.7.0-007ACC?style=for-the-badge&logo=visualstudiocode&logoColor=white" alt="Monaco Editor" />
  <img src="https://img.shields.io/badge/PayOS-Checkout-00A651?style=for-the-badge" alt="PayOS" />
  <img src="https://img.shields.io/badge/Status-Defense_Passed_%E2%9C%85-brightgreen?style=for-the-badge" alt="Status" />
</p>

---

## 📖 Overview

**APE_FE** is the official modern web client for the **APE (AI-Powered Examination and Programming Practice Platform)** — an enterprise-grade automated assessment and practice platform powered by an intelligent **Multi-Agent AI architecture**.

Built with **React 18** and **Vite**, the application seamlessly integrates the **Monaco Code Editor** (the engine powering Visual Studio Code), automated **PayOS QR payments**, and real-time gamified learning analytics. It delivers a comprehensive learning workspace for students and a robust administrative suite for instructors and system administrators.

---

## 📑 Table of Contents

- [Key Features](#-key-features)
  - [1. Student Learning & Exam Portal](#1-student-learning--exam-portal)
  - [2. Super Admin & Governance Portal](#2-super-admin--governance-portal)
- [Project Architecture](#-project-architecture)
- [Technology Stack](#-technology-stack)
- [Getting Started](#-getting-started)
  - [Prerequisites](#1-prerequisites)
  - [Environment Setup](#2-environment-setup)
  - [Installation & Local Execution](#3-installation--local-execution)
  - [Production Build](#4-production-build)
- [System Ecosystem & Backend Services](#-system-ecosystem--backend-services)
- [Capstone Project Information](#-capstone-project-information)

---

## ✨ Key Features

### 1. Student Learning & Exam Portal

- **Authentication & Session Security**:
  - Seamless Single Sign-On with Google OAuth 2.0 (`@react-oauth/google`).
  - Automatic JWT token refresh and secure session lifecycle management.

- **BYOS (Bring Your Own Source) Ingestion**:
  - Direct upload of course materials (`.pdf`, `.docx`, `.pptx`).
  - Real-time OCR visual transcription and automated Chapter/Topic structure detection preview.

- **On-Demand Practice & Exam Generation**:
  - Fully customizable parameters: Subject, Chapter, Topic tags, Question count, and Difficulty levels (*Easy, Medium, Hard*).
  - Supports dual exam formats: **FE (Multiple-Choice Questions with pedagogical rationales)** and **PE (Practical Coding Challenges with test suites)**.

- **Interactive Examination Workspaces**:
  - **FE Multiple-Choice Mode**: Countdown timer, instantaneous automated grading, answer rationales, and ground-truth citations.
  - **PE Practical Coding Mode**: Embedded **Monaco Code Editor**, real-time syntax highlighting, instant code compilation against a **Judge0** sandbox, with scoring across public and hidden test cases.
  - **AI Code Mentor**: Socratic debugging assistant that analyzes compiler and runtime errors, guiding students toward solutions without revealing direct answers.

- **AI Wallet & PayOS Payment Gateway**:
  - Instant balance top-up via dynamic VietQR codes powered by PayOS Checkout.
  - Transparent usage tracking: Granular per-request token usage logs and fair post-pay billing.

- **Gamification & Learning Analytics**:
  - Real-time Leaderboard, Learning Streaks, Achievement Badges, and Knowledge Mastery Radar Charts.

---

### 2. Super Admin & Governance Portal

- **Executive Analytics Dashboard**:
  - Real-time metrics on active users, exam sessions, revenue, and LLM token consumption across all providers.

- **Curricula & Document Governance**:
  - Course management, official syllabus uploads, and OCR extraction draft approvals.

- **AI Question Bank Management**:
  - Review AI-generated question drafts, approve and publish to the general question pool (`Active`), or reject defective items (`Disabled`).

- **User & Top-up Package Management**:
  - Role-based access control (Admin/Student) and dynamic top-up package price configuration.

- **Database-First AI Agent Monitoring & Configuration**:
  - Real-time health monitoring and latency tracking for all 5 AI Agents (*Gatekeeper, Generator, Reviewer, Mentor, Extraction*).
  - Dynamically update active LLM models, temperature, max token limits, and prompt templates with zero server downtime.
  - Built-in UI Smoke Testing for real-time provider verification.

---

## 🏗️ Project Architecture

```
APE_FE/
├── public/                 # Static assets (logos, branding, icons)
├── src/
│   ├── app/                # Application routing & layout dispatchers
│   │   ├── adminRoutes.jsx    # Super Admin route configurations
│   │   └── studentRoutes.jsx  # Student portal route configurations
│   ├── assets/             # Global styles, SVGs, and brand assets
│   ├── components/         # Reusable structural UI components (Navbar, Sidebar)
│   ├── lib/                # Storage adapters, Route constants, Formatting utilities
│   ├── modules/            # Domain-driven feature modules
│   │   ├── question-generation-byos/     # Personal document generation workflow
│   │   ├── question-generation-system/   # Curricula-based generation workflow
│   │   └── student-documents/            # Ingestion drafts & document viewer
│   ├── pages/              # View layer page components
│   │   ├── admin/             # Admin management views (Dashboard, AI Config, Users)
│   │   ├── auth/              # Authentication views (LoginPage)
│   │   ├── shared/            # Shared preview and approval views
│   │   └── student/           # Student views (Workspace, Exam, Wallet, Analytics)
│   ├── services/           # Backend API integration layer
│   │   ├── http.js            # Centralized Fetch wrapper with Bearer token injection
│   │   ├── authService.js     # Authentication, session, and token refresh
│   │   ├── questionService.js # Exam and question submission endpoints
│   │   ├── walletService.js   # Wallet top-up, PayOS gateway, and transaction logs
│   │   └── admin*.js          # Administrative API endpoints
│   ├── shared/             # Cross-cutting UI components and lifecycle guards
│   │   ├── guards/            # useInFlightGuard, useBeforeUnloadGuard
│   │   └── ui/                # ToastNotification, ConfirmModal, InFlightNotice
│   ├── App.jsx             # Root application component
│   ├── main.jsx            # Application bootstrap entry point
│   └── index.css           # Global design system & theme CSS variables
├── .env                    # Client environment configuration
├── package.json            # Project dependencies and script definitions
├── vite.config.js          # Vite build configuration and proxy settings
└── README.md               # Project documentation
```

---

## 🛠️ Technology Stack

| Category | Technology / Library | Purpose |
|---|---|---|
| **Core Framework** | React `18.3.1` | Declarative component-based user interface |
| **Build & Bundler** | Vite `5.4.10` | High-speed Hot Module Replacement (HMR) & production bundling |
| **Code Editor** | `@monaco-editor/react` (`4.7.0`) | In-browser VS Code editing experience for practical exams |
| **Payment Gateway** | `@payos/payos-checkout` (`1.0.8`) | Embedded VietQR automated checkout modal |
| **Styling & Theme** | Vanilla CSS (CSS Variables) | High-performance custom theme system (Dark/Light mode) |
| **Networking** | Native Fetch Wrapper (`services/http.js`) | Centralized HTTP request handling with JWT Bearer injection |

---

## 🚀 Getting Started

### 1. Prerequisites

- **Node.js**: Version `18.x` or `20.x` LTS ([Download](https://nodejs.org/)).
- **NPM** or **Yarn** package manager.
- **Backend Service**: Running instance of **APE_BE** (ASP.NET Core 8 Web API at `http://localhost:5292`).

---

### 2. Environment Setup

Verify or create the `.env` file in the `APE_FE/` root directory:

```env
# Base URL for APE_BE ASP.NET Core API
VITE_API_BASE_URL=http://localhost:5292/

# Google Identity Services OAuth 2.0 Client ID
VITE_GOOGLE_CLIENT_ID=your_google_client_id_here.apps.googleusercontent.com
```

---

### 3. Installation & Local Execution

#### Install Dependencies:
```bash
npm install
```

#### Start Development Server:
```bash
npm run dev
```
The application will be accessible at: **`http://localhost:5173/`**

---

### 4. Production Build

#### Build Production Bundle:
```bash
npm run build
```

#### Preview Production Build:
```bash
npm run preview
```

---

## 🔗 System Ecosystem & Backend Services

- **Backend Repository**: `APE_BE` — ASP.NET Core 8 Web API, MongoDB, Clean Architecture.
- **AI Multi-Agent Infrastructure**:
  - Primary LLM: OpenAI (`gpt-5.4`, `gpt-4o`)
  - Secondary/Fallback LLM: DeepSeek (`deepseek-v4-pro`, `deepseek-v4-flash`)
  - Vector Embedding: Cohere (`embed-multilingual-v3.0`, 1024 float dimensions)
- **Code Execution Sandbox**: Judge0 CE Sandbox API.
- **Payment Processing**: PayOS VietQR API.

---

## 🎓 Capstone Project Information

- **Project**: APE — AI-Powered Examination and Programming Practice Platform
- **Institution**: FPT University — Department of Computer Science & Software Engineering
- **Course**: SEP490 / Graduation Capstone Project
- **Outcome**: **Official Graduation Defense Passed on First Attempt ✅**
