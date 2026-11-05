from scripts.generate_placeholder_models import _pbr_for_finish, _FINISH_PBR, _DEFAULT_METALLIC_ROUGHNESS


def test_known_finish_returns_distinct_factors():
    color, metallic, roughness = _pbr_for_finish("matte_black", fallback_color=(0.5, 0.5, 0.5, 1.0))
    assert (color, metallic, roughness) == _FINISH_PBR["matte_black"]


def test_finish_lookup_is_case_insensitive():
    color, metallic, roughness = _pbr_for_finish("MATTE_BLACK", fallback_color=(0.5, 0.5, 0.5, 1.0))
    assert (color, metallic, roughness) == _FINISH_PBR["matte_black"]


def test_unknown_finish_falls_back_to_category_color_and_default_metallic_roughness():
    fallback = (0.11, 0.22, 0.33, 1.0)
    color, metallic, roughness = _pbr_for_finish("some_future_finish", fallback_color=fallback)
    assert color == fallback
    assert (metallic, roughness) == _DEFAULT_METALLIC_ROUGHNESS


def test_missing_finish_falls_back_same_as_unknown():
    fallback = (0.11, 0.22, 0.33, 1.0)
    color, metallic, roughness = _pbr_for_finish(None, fallback_color=fallback)
    assert color == fallback
    assert (metallic, roughness) == _DEFAULT_METALLIC_ROUGHNESS


def test_distinct_finishes_have_distinct_metallic_values():
    # Spot-check that a dielectric (wood/glass) and a true metal don't collide.
    assert _FINISH_PBR["natural_wood"][1] == 0.0
    assert _FINISH_PBR["chrome"][1] == 1.0
