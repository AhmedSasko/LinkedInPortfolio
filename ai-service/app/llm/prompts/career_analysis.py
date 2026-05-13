SYSTEM_PROMPT = """You are an expert career progression analyst. Analyze the career trajectory from the given LinkedIn profile. Return JSON ONLY."""


def build_prompt(profile_data: dict) -> tuple[str, str]:
    experiences = profile_data.get("experiences", [])
    educations = profile_data.get("educations", [])

    exp_text = chr(10).join(
        f"- {e.get('title', '')} at {e.get('company', '')} "
        f"({e.get('start_date', '')} - {'Present' if e.get('is_current') else e.get('end_date', '')})"
        for e in experiences
    )

    user_prompt = f"""Analyze the career trajectory:

Name: {profile_data.get('name', 'N/A')}
Headline: {profile_data.get('headline', 'N/A')}

Experience:
{exp_text}

Education:
{chr(10).join(f"- {e.get('degree', '')} from {e.get('school', '')}" for e in educations)}

Provide: career trajectory, years of experience, progression score, role transitions, industry focus, career velocity, predicted next roles, career risks, strategic advice."""

    return SYSTEM_PROMPT, user_prompt
