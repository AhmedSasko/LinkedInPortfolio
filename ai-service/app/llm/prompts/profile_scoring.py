SYSTEM_PROMPT = """You are an expert LinkedIn profile analyst. Score the given LinkedIn profile on a scale of 0-100.

You MUST return ONLY a JSON object with EXACTLY these field names (no other keys):
{
  "overall_score": <integer 0-100, weighted average of all categories>,
  "completeness_score": <integer 0-100, based on photo/headline/about/location/all sections>,
  "headline_score": <integer 0-100, specific role+value, not generic>,
  "about_score": <integer 0-100, narrative quality, keywords, 100-300 words ideal>,
  "experience_score": <integer 0-100, quantifiable achievements, action verbs, clear progression>,
  "skills_score": <integer 0-100, 15-25 optimal skills, aligned with headline/experience>,
  "education_score": <integer 0-100, relevant degrees and recent certifications>,
  "projects_score": <integer 0-100, demonstrated work and links>,
  "strengths": ["strength 1", "strength 2", "strength 3"],
  "improvements": ["improvement 1", "improvement 2", "improvement 3"]
}

Use EXACTLY these field names. No markdown, no explanation, no extra keys."""


def build_prompt(profile_data: dict) -> tuple[str, str]:
    experiences = profile_data.get("experiences", [])
    skills = profile_data.get("skills", [])
    educations = profile_data.get("educations", [])
    projects = profile_data.get("projects", [])
    certifications = profile_data.get("certifications", [])

    user_prompt = f"""Analyze this LinkedIn profile and provide a detailed score:

Name: {profile_data.get('name', 'N/A')}
Headline: {profile_data.get('headline', 'N/A')}
Location: {profile_data.get('location', 'N/A')}
Has Photo: {bool(profile_data.get('photo_url') or profile_data.get('photo_base64'))}
About ({len(profile_data.get('about', '') or '')} chars): {(profile_data.get('about') or '')[:500]}

Experience ({len(experiences)} positions):
{chr(10).join(f"- {e.get('title', '')} at {e.get('company', '')} ({'Current' if e.get('is_current') else 'Past'})" for e in experiences[:10])}

Skills ({len(skills)}): {', '.join(s.get('name', '') for s in skills[:20])}

Education ({len(educations)} entries):
{chr(10).join(f"- {e.get('degree', '')} from {e.get('school', '')}" for e in educations[:5])}

Projects ({len(projects)}): {', '.join(p.get('title', '') for p in projects[:5])}
Certifications ({len(certifications)}): {', '.join(c.get('name', '') for c in certifications[:5])}"""

    return SYSTEM_PROMPT, user_prompt
