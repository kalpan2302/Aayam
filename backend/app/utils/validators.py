import re

def validate_sku(sku: str) -> bool:
    return bool(re.match(r'^[A-Z0-9_-]{3,100}$', sku))

def validate_slug(slug: str) -> bool:
    return bool(re.match(r'^[a-z0-9]+(?:-[a-z0-9]+)*$', slug))

def sanitize_search_query(query: str) -> str:
    if not query:
        return ""
    return query.replace("%", "\\\\%").replace("_", "\\\\_")

def clamp_page_size(page_size: int, max_size: int = 100) -> int:
    if page_size < 1:
        return 1
    if page_size > max_size:
        return max_size
    return page_size
