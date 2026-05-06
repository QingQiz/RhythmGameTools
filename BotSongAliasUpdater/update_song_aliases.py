from __future__ import annotations

import argparse
import csv
import json
import os
import re
import sys
from dataclasses import dataclass
from pathlib import Path
from typing import Iterable

import requests
from dotenv import load_dotenv
from opencc import OpenCC

PROJECT_ROOT = Path(__file__).resolve().parent
DEFAULT_BOT_ROOT = r"E:\MarisaBot"
DEFAULT_BASE_URL = "https://api.deepseek.com"
DEFAULT_MODEL = "deepseek-v4-flash"
DEFAULT_CACHE_PATH = PROJECT_ROOT / "translation_cache.json"
WINDOWS_PATH_PATTERN = re.compile(r"^[A-Za-z]:[\\/]")
HAN_PATTERN = re.compile(r"[\u3400-\u9FFF\uF900-\uFAFF]")
KANA_PATTERN = re.compile(r"[\u3040-\u30FF]")
PLACEHOLDER_API_KEYS = {"", "your_deepseek_api_key_here"}


@dataclass(frozen=True)
class GameConfig:
    name: str
    alias_relative_path: Path
    song_info_relative_path: Path


GAME_CONFIGS = (
    GameConfig(
        name="maimai",
        alias_relative_path=Path("Marisa.Frontend/public/assets/maimai/aliases.tsv"),
        song_info_relative_path=Path("Marisa.Frontend/public/assets/maimai/SongInfo.json"),
    ),
    GameConfig(
        name="chunithm",
        alias_relative_path=Path("Marisa.Frontend/public/assets/chunithm/aliases.tsv"),
        song_info_relative_path=Path("Marisa.Frontend/public/assets/chunithm/SongInfo.json"),
    ),
)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Update MarisaBot song aliases with OpenCC normalization and cached DeepSeek Chinese titles."
    )
    parser.add_argument(
        "--bot-root",
        default=os.getenv("MARISA_BOT_ROOT", DEFAULT_BOT_ROOT),
        help="Path to the MarisaBot repository (Windows or WSL path).",
    )
    parser.add_argument(
        "--cache-file",
        default=os.getenv("TRANSLATION_CACHE_FILE", str(DEFAULT_CACHE_PATH)),
        help="Path to the persistent translated-title cache JSON file.",
    )
    parser.add_argument(
        "--game",
        choices=[config.name for config in GAME_CONFIGS] + ["all"],
        default="all",
        help="Only update one game instead of both.",
    )
    parser.add_argument(
        "--limit",
        type=int,
        default=None,
        help="Only process the first N titles per selected game (handy for testing).",
    )
    parser.add_argument(
        "--dry-run",
        action="store_true",
        help="Read everything and print a summary without writing files or calling the translation API.",
    )
    parser.add_argument(
        "--batch-size",
        type=int,
        default=50,
        help="How many titles to send to the LLM in one batch request.",
    )
    args = parser.parse_args()
    if args.batch_size <= 0:
        parser.error("--batch-size must be greater than 0")
    return args


def normalize_text(value: str) -> str:
    return re.sub(r"\s+", " ", value).strip()


def has_han(value: str) -> bool:
    return bool(HAN_PATTERN.search(value))


def has_kana(value: str) -> bool:
    return bool(KANA_PATTERN.search(value))


def resolve_path(raw_path: str) -> Path:
    if os.name != "nt" and WINDOWS_PATH_PATTERN.match(raw_path):
        drive = raw_path[0].lower()
        tail = raw_path[2:].replace("\\", "/").lstrip("/")
        path = Path("/mnt") / drive / tail
    else:
        path = Path(raw_path).expanduser()

    return path if path.is_absolute() else PROJECT_ROOT / path


class OpenCcSimplifier:
    def __init__(self) -> None:
        self._converters: list[OpenCC] = []

        for config_candidates in (("jp2t", "jp2t.json"), ("t2s", "t2s.json")):
            for config_name in config_candidates:
                try:
                    self._converters.append(OpenCC(config_name))
                    break
                except Exception:
                    continue

        if not self._converters:
            raise RuntimeError("Unable to initialize OpenCC. Please install a package that provides OpenCC configs.")

    def convert(self, value: str) -> str:
        result = value
        for converter in self._converters:
            result = converter.convert(result)
        return normalize_text(result)


class TranslationCache:
    def __init__(self, path: Path) -> None:
        self.path = path
        self.data = self._load(path)

    @staticmethod
    def _load(path: Path) -> dict[str, str]:
        if not path.exists():
            return {}

        raw = json.loads(path.read_text(encoding="utf-8"))

        if isinstance(raw, dict) and all(isinstance(key, str) and isinstance(value, str) for key, value in raw.items()):
            return dict(raw)

        if isinstance(raw, dict) and isinstance(raw.get("titles"), dict):
            titles = raw["titles"]
            return {str(key): str(value) for key, value in titles.items()}

        raise ValueError(f"Unsupported cache format in {path}")

    def get(self, title: str) -> str | None:
        return self.data.get(title)

    def set(self, title: str, translated_title: str) -> None:
        self.data[title] = translated_title

    def save(self) -> None:
        self.path.parent.mkdir(parents=True, exist_ok=True)
        self.path.write_text(
            json.dumps(self.data, ensure_ascii=False, indent=2, sort_keys=True) + "\n",
            encoding="utf-8",
        )


class DeepSeekTranslator:
    def __init__(self, api_key: str | None, base_url: str, model: str, timeout_seconds: int = 60) -> None:
        self.api_key = normalize_text(api_key or "")
        self.base_url = base_url.rstrip("/")
        self.model = model
        self.timeout_seconds = timeout_seconds
        self._session = requests.Session()

    @property
    def enabled(self) -> bool:
        return self.api_key not in PLACEHOLDER_API_KEYS

    def _post_chat_completion(self, payload: dict) -> dict:
        response = self._session.post(
            f"{self.base_url}/chat/completions",
            headers={
                "Authorization": f"Bearer {self.api_key}",
                "Content-Type": "application/json",
                "Accept": "application/json",
            },
            json=payload,
            timeout=self.timeout_seconds,
        )
        response.raise_for_status()
        return response.json()

    @staticmethod
    def _extract_message_content(data: dict) -> str:
        try:
            message = data["choices"][0]["message"]
            content = message["content"]
        except (KeyError, IndexError, TypeError) as exc:
            raise RuntimeError(f"Unexpected DeepSeek response: {json.dumps(data, ensure_ascii=False)}") from exc

        if not isinstance(content, str):
            raise RuntimeError(f"Unexpected translation payload: {content!r}")

        normalized = normalize_text(content)
        if not normalized:
            raise RuntimeError(f"DeepSeek returned empty final content: {json.dumps(message, ensure_ascii=False)}")

        return normalized

    def translate_title(self, title: str) -> str:
        if not self.enabled:
            raise RuntimeError("DEEPSEEK_API_KEY is not configured.")

        payload = {
            "model": self.model,
            "messages": [
                {
                    "role": "system",
                    "content": (
                        "You translate rhythm game song titles into concise Simplified Chinese aliases. "
                        "Return exactly one title, with no explanation, no quotes, and no extra punctuation."
                    ),
                },
                {
                    "role": "user",
                    "content": (
                        f"Original title: {title}\n"
                        "Rules:\n"
                        "1. Use common or natural Simplified Chinese if one exists.\n"
                        "2. If the title is already Chinese, keep the meaning and normalize it to Simplified Chinese.\n"
                        "3. If the title is a stylized proper noun that should stay unchanged, return the original title.\n"
                        "4. Return only the translated title text."
                    ),
                },
            ],
            "thinking": {"type": "disabled"},
            "temperature": 0,
            "max_tokens": 64,
            "stream": False,
            "response_format": {"type": "text"},
        }

        data = self._post_chat_completion(payload)
        content = self._extract_message_content(data)
        return normalize_text(content.strip().strip('"').strip("'"))

    def translate_titles(self, titles: list[str]) -> dict[str, str]:
        if not titles:
            return {}
        if not self.enabled:
            raise RuntimeError("DEEPSEEK_API_KEY is not configured.")

        payload = {
            "model": self.model,
            "messages": [
                {
                    "role": "system",
                    "content": (
                        "You translate rhythm game song titles into concise Simplified Chinese aliases and return json only. "
                        "Return a json object with a top-level key named translations. "
                        "Each key inside translations must exactly match the original title, and each value must be the final Simplified Chinese translation string. "
                        "Do not omit any title. If a stylized proper noun should stay unchanged, use the original title as the value."
                    ),
                },
                {
                    "role": "user",
                    "content": json.dumps(
                        {
                            "task": "Translate every title to concise Simplified Chinese and return json only.",
                            "format_example": {
                                "translations": {
                                    "Title A": "翻译A",
                                    "Title B": "翻译B",
                                }
                            },
                            "titles": titles,
                        },
                        ensure_ascii=False,
                    ),
                },
            ],
            "thinking": {"type": "disabled"},
            "temperature": 0,
            "max_tokens": max(512, min(8192, len(titles) * 96)),
            "stream": False,
            "response_format": {"type": "json_object"},
        }

        data = self._post_chat_completion(payload)
        content = self._extract_message_content(data)

        try:
            parsed = json.loads(content)
        except json.JSONDecodeError as exc:
            raise RuntimeError(f"DeepSeek returned invalid JSON content: {content}") from exc

        raw_translations = parsed.get("translations", parsed)
        if not isinstance(raw_translations, dict):
            raise RuntimeError(f"DeepSeek JSON output missing translations mapping: {content}")

        translations: dict[str, str] = {}
        for title in titles:
            translated = raw_translations.get(title)
            if not isinstance(translated, str) or not normalize_text(translated):
                raise RuntimeError(f"DeepSeek batch output missing translation for {title!r}: {content}")
            translations[title] = normalize_text(translated)

        return translations

    def translate_titles_batched(self, titles: list[str], batch_size: int) -> dict[str, str]:
        translations: dict[str, str] = {}

        unique_titles: list[str] = []
        seen: set[str] = set()
        for title in titles:
            if title in seen:
                continue
            seen.add(title)
            unique_titles.append(title)

        batch_total = (len(unique_titles) + batch_size - 1) // batch_size
        for batch_index, start in enumerate(range(0, len(unique_titles), batch_size), start=1):
            batch = unique_titles[start:start + batch_size]
            print(f"Translating batch {batch_index}/{batch_total} ({len(batch)} titles)...")
            try:
                translations.update(self.translate_titles(batch))
            except Exception:
                print("Batch translation failed, falling back to single-title requests for this batch...", file=sys.stderr)
                for title in batch:
                    translations[title] = self.translate_title(title)

        return translations


def load_alias_table(path: Path) -> dict[str, list[str]]:
    rows: dict[str, list[str]] = {}

    with path.open("r", encoding="utf-8-sig", newline="") as handle:
        reader = csv.reader(handle, delimiter="\t", quotechar='"')
        for row in reader:
            if not row:
                continue

            title = row[0]
            bucket = rows.setdefault(title, [])
            for alias in row[1:]:
                normalized_alias = normalize_text(alias)
                if normalized_alias:
                    bucket.append(normalized_alias)

    return rows


def write_alias_table(path: Path, table: dict[str, list[str]]) -> None:
    with path.open("w", encoding="utf-8", newline="") as handle:
        writer = csv.writer(
            handle,
            delimiter="\t",
            quotechar='"',
            lineterminator="\n",
            quoting=csv.QUOTE_MINIMAL,
        )
        for title, aliases in sorted(table.items()):
            if not aliases:
                continue
            writer.writerow([title, *aliases])


def cleanup_aliases(title: str, aliases: Iterable[str]) -> list[str]:
    cleaned: list[str] = []
    seen: set[str] = set()

    for alias in aliases:
        normalized_alias = normalize_text(alias)
        if not normalized_alias:
            continue

        folded = normalized_alias.casefold()
        if folded in seen:
            continue

        seen.add(folded)
        cleaned.append(normalized_alias)

    to_remove: set[int] = set()
    title_folded = title.casefold()

    for index, alias in enumerate(cleaned):
        alias_folded = alias.casefold()
        if alias_folded == title_folded:
            to_remove.add(index)
            continue

        for previous_index in range(index):
            if previous_index in to_remove:
                continue
            if alias_folded in cleaned[previous_index].casefold():
                to_remove.add(index)
                break

    result = [alias for index, alias in enumerate(cleaned) if index not in to_remove]
    result.sort()
    return result


def contains_alias(aliases: Iterable[str], candidate: str) -> bool:
    candidate_folded = candidate.casefold()
    return any(normalize_text(alias).casefold() == candidate_folded for alias in aliases)


def extract_titles(song_info_path: Path) -> list[str]:
    raw_data = json.loads(song_info_path.read_text(encoding="utf-8-sig"))

    if not isinstance(raw_data, list):
        raise ValueError(f"Expected a JSON array in {song_info_path}")

    titles: list[str] = []
    seen: set[str] = set()

    for entry in raw_data:
        if not isinstance(entry, dict):
            continue

        title = entry.get("title") or entry.get("Title")
        if not title:
            basic_info = entry.get("basic_info")
            if isinstance(basic_info, dict):
                title = basic_info.get("title")

        if not isinstance(title, str):
            continue

        normalized_title = normalize_text(title)
        if not normalized_title or normalized_title in seen:
            continue

        seen.add(normalized_title)
        titles.append(normalized_title)

    return titles


def default_opencc_alias(title: str, simplifier: OpenCcSimplifier) -> str | None:
    simplified_title = simplifier.convert(title)
    if simplified_title and simplified_title.casefold() != title.casefold():
        return simplified_title
    return None


def update_game(
    config: GameConfig,
    bot_root: Path,
    simplifier: OpenCcSimplifier,
    translator: DeepSeekTranslator,
    cache: TranslationCache,
    dry_run: bool,
    limit: int | None,
    batch_size: int,
) -> dict[str, int]:
    alias_path = bot_root / config.alias_relative_path
    song_info_path = bot_root / config.song_info_relative_path

    if not alias_path.exists():
        raise FileNotFoundError(f"Alias file not found: {alias_path}")
    if not song_info_path.exists():
        raise FileNotFoundError(f"Song info file not found: {song_info_path}")

    table = load_alias_table(alias_path)
    titles = extract_titles(song_info_path)
    if limit is not None:
        titles = titles[:limit]

    stats = {
        "titles": len(titles),
        "new_rows": 0,
        "opencc_aliases": 0,
        "translated_from_cache": 0,
        "translated_from_api": 0,
        "translation_skipped": 0,
    }

    translation_sources: dict[str, str] = {}
    uncached_titles = [title for title in titles if cache.get(title) is None]

    for title in titles:
        if cache.get(title):
            translation_sources[title] = "cache"

    if not dry_run and translator.enabled and uncached_titles:
        translated_batch = translator.translate_titles_batched(uncached_titles, batch_size)
        for title, translated in translated_batch.items():
            normalized_translation = simplifier.convert(translated)
            if normalized_translation:
                cache.set(title, normalized_translation)
                translation_sources[title] = "api"

    for title in uncached_titles:
        translation_sources.setdefault(title, "skipped")

    for title in titles:
        is_new_row = title not in table
        aliases = table.setdefault(title, [])
        if is_new_row:
            stats["new_rows"] += 1

        opencc_alias = default_opencc_alias(title, simplifier)
        if opencc_alias and not contains_alias(aliases, opencc_alias):
            aliases.append(opencc_alias)
            stats["opencc_aliases"] += 1

        translated_alias = cache.get(title)
        if translated_alias and not contains_alias(aliases, translated_alias):
            aliases.append(translated_alias)

        source = translation_sources.get(title, "skipped")
        if source == "cache":
            stats["translated_from_cache"] += 1
        elif source == "api":
            stats["translated_from_api"] += 1
        else:
            stats["translation_skipped"] += 1

        table[title] = cleanup_aliases(title, aliases)

    for existing_title, aliases in list(table.items()):
        table[existing_title] = cleanup_aliases(existing_title, aliases)

    if not dry_run:
        write_alias_table(alias_path, table)

    return stats


def print_summary(game_name: str, stats: dict[str, int], dry_run: bool) -> None:
    mode = "dry-run" if dry_run else "updated"
    print(
        f"[{game_name}] {mode}: "
        f"titles={stats['titles']}, "
        f"new_rows={stats['new_rows']}, "
        f"opencc_aliases={stats['opencc_aliases']}, "
        f"cache={stats['translated_from_cache']}, "
        f"api={stats['translated_from_api']}, "
        f"skipped={stats['translation_skipped']}"
    )


def main() -> int:
    load_dotenv(PROJECT_ROOT / ".env", override=True)
    args = parse_args()

    bot_root = resolve_path(args.bot_root)
    cache_path = resolve_path(args.cache_file)

    simplifier = OpenCcSimplifier()
    cache = TranslationCache(cache_path)
    translator = DeepSeekTranslator(
        api_key=os.getenv("DEEPSEEK_API_KEY"),
        base_url=os.getenv("DEEPSEEK_BASE_URL", DEFAULT_BASE_URL),
        model=os.getenv("DEEPSEEK_MODEL", DEFAULT_MODEL),
    )

    selected_configs = GAME_CONFIGS if args.game == "all" else tuple(config for config in GAME_CONFIGS if config.name == args.game)

    if not translator.enabled and not args.dry_run:
        print(
            "Warning: DEEPSEEK_API_KEY is not configured. "
            "The updater will reuse cached/existing Chinese aliases and skip uncached translations.",
            file=sys.stderr,
        )

    for config in selected_configs:
        stats = update_game(
            config=config,
            bot_root=bot_root,
            simplifier=simplifier,
            translator=translator,
            cache=cache,
            dry_run=args.dry_run,
            limit=args.limit,
            batch_size=args.batch_size,
        )
        print_summary(config.name, stats, args.dry_run)

    if not args.dry_run:
        cache.save()

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
