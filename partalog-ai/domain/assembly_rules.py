"""Assembly-level source selection rules used after retrieval."""

from dataclasses import dataclass


@dataclass(frozen=True)
class SourceGroupRule:
    name: str
    needles: tuple[str, ...]
    name_only: bool = False
    exact_name: bool = False


@dataclass(frozen=True)
class AssemblySourceRule:
    label: str
    triggers: tuple[str, ...]
    groups: tuple[SourceGroupRule, ...]


CONTEXT_SOURCE_RULES: tuple[AssemblySourceRule, ...] = (
    AssemblySourceRule(
        label="kayar kapak",
        triggers=("kayar kapak",),
        groups=(
            SourceGroupRule("kayar_kapak", ("kayar", "slide"), name_only=True),
            SourceGroupRule("kapak_destek", ("kapak destek", "slide cover support", "plaka destek")),
            SourceGroupRule("kapak_pimi", ("kapak pimi", "pin for cover")),
        ),
    ),
    AssemblySourceRule(
        label="iplik kılavuzu",
        triggers=("iplik kilavuzu",),
        groups=(
            SourceGroupRule("iplik_kilavuzu", ("iplik guzergahi", "iplik güzergahı", "thread guide"), name_only=True),
        ),
    ),
    AssemblySourceRule(
        label="kapak destek",
        triggers=("kapak destek",),
        groups=(
            SourceGroupRule("kapak_destek", ("kapak destek", "slide cover support", "plaka destek")),
        ),
    ),
    AssemblySourceRule(
        label="kapak pimi",
        triggers=("kapak pimi",),
        groups=(
            SourceGroupRule("kapak_pimi", ("kapak pimi", "pin for cover")),
        ),
    ),
    AssemblySourceRule(
        label="menteşe",
        triggers=("menteşe", "mentese"),
        groups=(
            SourceGroupRule("mentese", ("mentese", "menteşe", "hinge"), name_only=True),
        ),
    ),
    AssemblySourceRule(
        label="arka plaka",
        triggers=("arka plaka",),
        groups=(
            SourceGroupRule("arka_plaka", ("plaka arka", "arka plaka", "cloth plate rear"), name_only=True),
        ),
    ),
    AssemblySourceRule(
        label="ön kapak",
        triggers=("asagi acilir kapak", "aşağı açılır kapak"),
        groups=(
            SourceGroupRule("asagi_acilir_kapak", ("salincak kapak", "asagi acilir kapak", "aşağı açılır kapak", "swing down cover"), name_only=True),
            SourceGroupRule("kapak_pimi", ("kapak pimi", "pin for cover")),
            SourceGroupRule("mentese", ("mentese", "menteşe", "hinge"), name_only=True),
        ),
    ),
    AssemblySourceRule(
        label="kumaş plaka",
        triggers=("kumas plaka", "kumaş plaka"),
        groups=(
            SourceGroupRule("plaka_montaj", ("plaka montaj", "cloth plate assy"), name_only=True),
            SourceGroupRule("arka_plaka", ("plaka arka", "arka plaka", "cloth plate rear"), name_only=True),
            SourceGroupRule("ana_plaka", ("plaka", "cloth plate"), name_only=True, exact_name=True),
            SourceGroupRule("plaka_blok", ("plaka blok", "cloth plate block"), name_only=True),
        ),
    ),
    AssemblySourceRule(
        label="plaka blok",
        triggers=("plaka blok",),
        groups=(
            SourceGroupRule("plaka_blok", ("plaka blok", "cloth plate block"), name_only=True),
        ),
    ),
)
