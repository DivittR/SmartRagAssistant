# Smart RAG Document & Syllabus Assistant 📄🤖

A lightweight, high-performance Retrieval-Augmented Generation (RAG) application that allows users to upload complex PDF documents (like academic syllabi, placement manuals, or technical guides) and instantly query them using AI. 

Built to eliminate the need to manually sift through dense, multi-page PDFs, this tool extracts relevant context and leverages Google's Gemini AI to generate precise, page-cited answers.

## ✨ Features
* **Instant Document Processing:** Upload and parse PDFs entirely locally using `PdfPig`, avoiding slow external vector database uploads.
* **Smart Keyword Scoring:** Uses a custom, ultra-fast local keyword extraction and scoring algorithm to find the most relevant document chunks instantly.
* **AI-Powered Q&A:** Direct REST API integration with Google's **Gemini 3.6 Flash** model to reason over the extracted text and solve logical/coding problems based on document context.
* **Clean UI:** A responsive, single-page application built with HTML, CSS (Bootstrap), and vanilla JavaScript.
* **Secure Credentials:** Utilizes .NET Secret Manager to keep API keys hidden and out of source control.

## 🛠️ Tech Stack
* **Backend:** C# .NET Minimal APIs
* **PDF Extraction:** PdfPig
* **AI Integration:** Google Gemini API (Direct HTTP/REST)
* **Frontend:** HTML5, Bootstrap 5, JavaScript (Fetch API)

## 🚀 Getting Started

### Prerequisites
* [.NET 8.0 SDK](https://dotnet.microsoft.com/download) (or later) installed on your machine.
* A free Gemini API Key from [Google AI Studio](https://aistudio.google.com/).

### Installation & Setup
1. **Clone the repository**
   ```bash
   git clone [https://github.com/DivittR/SmartRagAssistant.git](https://github.com/DivittR/SmartRagAssistant.git)
   cd SmartRagAssistant
