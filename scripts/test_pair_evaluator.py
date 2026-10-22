import importlib.util
import pathlib
import unittest

spec = importlib.util.spec_from_file_location("pair_eval", pathlib.Path(__file__).with_name("evaluate-pair-captures.py"))
evaluator = importlib.util.module_from_spec(spec)
spec.loader.exec_module(evaluator)


class RankingMetricTests(unittest.TestCase):
    def test_unjudged_is_rejected_instead_of_becoming_negative(self):
        with self.assertRaisesRegex(ValueError, "unlabeled"):
            evaluator.ranking_metrics(["unjudged"], {"relevant": 2}, 3)

    def test_reranking_improves_order_but_cannot_recover_missed_positive(self):
        labels = {"a": 2, "b": 0, "missed": 2}
        before = evaluator.ranking_metrics(["b", "a"], labels, 2)
        after = evaluator.ranking_metrics(["a", "b"], labels, 2)
        self.assertEqual(.5, before["selectionRecall"])
        self.assertEqual(before["selectionRecall"], after["selectionRecall"])
        self.assertGreater(after["ndcgAtK"], before["ndcgAtK"])
        self.assertLess(after["ndcgAtK"], 1)

    def test_no_positive_has_undefined_recall(self):
        result = evaluator.ranking_metrics(["a"], {"a": 0}, 3)
        self.assertIsNone(result["selectionRecall"])
        self.assertIsNone(result["ndcgAtK"])
        self.assertEqual(0, result["top1Relevant"])

    def test_duplicate_selected_pair_cannot_inflate_recall(self):
        with self.assertRaises(ValueError):
            evaluator.ranking_metrics(["a", "a"], {"a": 2}, 3)


if __name__ == "__main__":
    unittest.main()
