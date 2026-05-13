import re
import structlog
from typing import Optional

log = structlog.get_logger()


def extract_email(text: str) -> Optional[str]:
    match = re.search(r"[a-zA-Z0-9._%+\-]+@[a-zA-Z0-9.\-]+\.[a-zA-Z]{2,}", text)
    return match.group(0) if match else None


def extract_phone(text: str) -> Optional[str]:
    match = re.search(r"[\+]?[\d\s\-\(\)\.]{7,18}", text)
    return match.group(0).strip() if match else None


def extract_urls(text: str) -> list[str]:
    pattern = r"https?://[^\s]+"
    return re.findall(pattern, text)


def extract_name(text: str) -> Optional[str]:
    """Heuristic: first line that looks like a name (2-4 words, no digits)."""
    for line in text.split("\n"):
        line = line.strip()
        if not line:
            continue
        words = line.split()
        if 2 <= len(words) <= 4 and not any(c.isdigit() for c in line):
            return line
    return None


def extract_entities_with_spacy(text: str, model: str = "en_core_web_sm") -> dict:
    """Use spaCy for named entity extraction from resume text."""
    result: dict = {"persons": [], "organizations": [], "locations": [], "misc": []}
    try:
        import spacy
        nlp = spacy.load(model)
        doc = nlp(text[:5000])
        for ent in doc.ents:
            if ent.label_ == "PERSON":
                result["persons"].append(ent.text.strip())
            elif ent.label_ == "ORG":
                result["organizations"].append(ent.text.strip())
            elif ent.label_ in ("GPE", "LOC"):
                result["locations"].append(ent.text.strip())
            else:
                result["misc"].append(ent.text.strip())
    except Exception as e:
        log.warning("entity_recognizer.spacy_unavailable", error=str(e))
    return result
