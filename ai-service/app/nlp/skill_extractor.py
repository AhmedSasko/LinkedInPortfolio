import structlog
from typing import Optional

log = structlog.get_logger()

TECH_SKILLS = {
    "python", "java", "javascript", "typescript", "go", "rust", "c++", "c#", "swift",
    "kotlin", "ruby", "php", "scala", "r", "matlab", "bash", "powershell",
    "react", "angular", "vue", "next.js", "nuxt", "svelte", "tailwind",
    "node.js", "express", "fastapi", "django", "flask", "spring boot", "asp.net",
    "graphql", "rest api", "grpc", "websocket",
    "mysql", "postgresql", "mongodb", "redis", "elasticsearch", "cassandra", "sqlite",
    "docker", "kubernetes", "terraform", "ansible", "jenkins", "github actions",
    "aws", "azure", "gcp", "cloudflare", "nginx", "apache",
    "machine learning", "deep learning", "nlp", "computer vision", "llm",
    "tensorflow", "pytorch", "keras", "scikit-learn", "hugging face",
    "pandas", "numpy", "spark", "hadoop", "kafka", "airflow",
    "git", "linux", "agile", "scrum", "jira", "figma",
    "microservices", "ci/cd", "devops", "mlops", "data engineering",
}

SOFT_SKILLS = {
    "leadership", "communication", "teamwork", "problem solving", "critical thinking",
    "project management", "mentoring", "collaboration", "adaptability", "creativity",
    "time management", "negotiation", "presentation", "analytical", "strategic thinking",
}


def extract_skills_from_text(text: str, include_soft: bool = False) -> list[str]:
    """Extract skills from raw text using keyword matching."""
    text_lower = text.lower()
    found = []
    for skill in TECH_SKILLS:
        if skill in text_lower:
            found.append(skill)
    if include_soft:
        for skill in SOFT_SKILLS:
            if skill in text_lower:
                found.append(skill)
    return sorted(set(found))


def extract_skills_with_spacy(text: str, model: str = "en_core_web_sm") -> list[str]:
    """Enhanced extraction combining keyword matching with spaCy NER."""
    keyword_skills = set(extract_skills_from_text(text, include_soft=True))
    try:
        import spacy
        nlp = spacy.load(model)
        doc = nlp(text[:6000])
        spacy_entities = {
            ent.text.strip()
            for ent in doc.ents
            if ent.label_ in ("ORG", "PRODUCT") and 2 <= len(ent.text.strip()) <= 40
        }
        return sorted(keyword_skills | spacy_entities)
    except Exception as e:
        log.warning("skill_extractor.spacy_unavailable", error=str(e))
        return sorted(keyword_skills)
