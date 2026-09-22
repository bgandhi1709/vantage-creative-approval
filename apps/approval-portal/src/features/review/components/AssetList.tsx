import type { CreativeAsset } from '../api/reviewApi';

interface AssetListProps {
  assets: CreativeAsset[];
  heading: string;
}

/** Presentational: takes props, renders, raises nothing. */
export function AssetList({ assets, heading }: AssetListProps) {
  return (
    <section className="assets">
      <h2>{heading}</h2>
      <ul className="assets__list">
        {assets.map((asset) => (
          <li key={asset.assetId} className="assets__item">
            <span className="assets__name">{asset.name}</span>
            <span className="assets__format">{asset.format}</span>
          </li>
        ))}
      </ul>
    </section>
  );
}
