import base64
import io
import re
import structlog
from app.schemas.resume import ResumeParseResult

log = structlog.get_logger()


def _extract_text_from_pdf(content: bytes) -> str:
    import pdfplumber
    text_parts = []
    with pdfplumber.open(io.BytesIO(content)) as pdf:
        for page in pdf.pages:
            text = page.extract_text()
            if text:
                text_parts.append(text)
    return "\n".join(text_parts)


def _extract_text_from_docx(content: bytes) -> str:
    from docx import Document
    doc = Document(io.BytesIO(content))
    return "\n".join(p.text for p in doc.paragraphs if p.text.strip())


def _extract_email(text: str) -> str | None:
    match = re.search(r"[a-zA-Z0-9._%+\-]+@[a-zA-Z0-9.\-]+\.[a-zA-Z]{2,}", text)
    return match.group(0) if match else None


def _extract_phone(text: str) -> str | None:
    match = re.search(r"[\+]?[\d\s\-\(\)]{7,15}", text)
    return match.group(0).strip() if match else None


def _extract_name_from_text(text: str) -> str | None:
    """Try to get name from the first non-empty line."""
    lines = [l.strip() for l in text.split("\n") if l.strip()]
    if lines:
        first = lines[0]
        # Likely a name if it has 2-4 words and no digits
        words = first.split()
        if 2 <= len(words) <= 4 and not any(c.isdigit() for c in first):
            return first
    return None


def _extract_skills_with_spacy(text: str, spacy_model: str = "en_core_web_sm") -> list[str]:
    try:
        import spacy
        nlp = spacy.load(spacy_model)
        doc = nlp(text[:5000])  # limit for performance

        # Common tech skill keywords heuristic
        skill_keywords = {
            "python", "java", "javascript", "typescript", "react", "angular", "vue",
            "node", "django", "fastapi", "flask", "spring", "sql", "mysql", "postgresql",
            "mongodb", "redis", "docker", "kubernetes", "aws", "azure", "gcp", "git",
            "linux", "bash", "c++", "c#", ".net", "go", "rust", "swift", "kotlin",
            "machine learning", "deep learning", "tensorflow", "pytorch", "pandas",
            "numpy", "scikit-learn", "nlp", "data science", "agile", "scrum",
        }

        found = set()
        text_lower = text.lower()
        for skill in skill_keywords:
            if skill in text_lower:
                found.add(skill)

        # Add NER-detected ORG/PRODUCT entities as potential skills
        for ent in doc.ents:
            if ent.label_ in ("ORG", "PRODUCT") and len(ent.text) < 30:
                found.add(ent.text.strip())

        return sorted(found)
    except Exception as e:
        log.warning("resume_parser.spacy_failed", error=str(e))
        return []


class ResumeParser:
    def __init__(self, spacy_model: str = "en_core_web_sm"):
        self.spacy_model = spacy_model

    def parse(self, file_content_base64: str, file_type: str) -> ResumeParseResult:
        content = base64.b64decode(file_content_base64)
        file_type = file_type.lower().lstrip(".")

        if file_type == "pdf":
            raw_text = _extract_text_from_pdf(content)
        elif file_type in ("docx", "doc"):
            raw_text = _extract_text_from_docx(content)
        else:
            raise ValueError(f"Unsupported file type: {file_type}")

        skills = _extract_skills_with_spacy(raw_text, self.spacy_model)

        return ResumeParseResult(
            name=_extract_name_from_text(raw_text),
            email=_extract_email(raw_text),
            phone=_extract_phone(raw_text),
            skills=skills,
            raw_text=raw_text[:2000],  # store truncated raw text
        )
